import sys
sys.path.append("../../../DeepLearningONNX")

import Library.Utility as utility
import Library.AdamWR.adamw as adamw
import Library.AdamWR.cyclic_scheduler as cyclic_scheduler
import Models.GNN.InBetweeningNetwork as this

import numpy as np
import torch
from torch.nn.parameter import Parameter
import torch.nn.functional as F

if __name__ == '__main__':
    load = "../../dataset/LaFAN1_NoObstacle_DeepPhases_Styles"
    save = "./Training"

    InputFile = load + "/Input.txt"
    OutputFile = load + "/Output.txt"
    Xnorm = utility.ReadNorm(load + "/InputNorm.txt")
    Ynorm = utility.ReadNorm(load + "/OutputNorm.txt")

    utility.SetSeed(23456)

    epochs = 150
    batch_size = 32
    dropout = 0.3
    gating_hidden = 128
    main_hidden = 512
    experts = 8

    learning_rate = 1e-4
    weight_decay = 1e-4
    restart_period = 10
    restart_mult = 2

    print(torch.__version__)
    print(torch.cuda.is_available())
    print(torch.cuda.device_count())
    print(torch.cuda.current_device())
    print(torch.cuda.get_device_name(0))

    print("Started creating data pointers...")
    pointersX = utility.CollectPointers(InputFile)
    pointersY = utility.CollectPointers(OutputFile)
    print("Finished creating data pointers.")

    sample_count = pointersX.shape[0]
    input_dim = Xnorm.shape[1]
    output_dim = Ynorm.shape[1]
    
    #SpectralModel
    gating_indices = torch.tensor([(693 + i) for i in range(130)]) #index where phase starts
    main_indices = torch.tensor([(i) for i in range(693)])

    network = utility.ToDevice(this.Model(
        gating_indices=gating_indices, 
        gating_input=len(gating_indices), 
        gating_hidden=gating_hidden, 
        gating_output=experts, 
        main_indices=main_indices, 
        main_input=len(main_indices), 
        main_hidden=main_hidden, 
        main_output=output_dim,
        dropout=dropout,
        input_norm=Xnorm,
        output_norm=Ynorm
    ))

    optimizer = adamw.AdamW(network.parameters(), lr=learning_rate, weight_decay=weight_decay)
    scheduler = cyclic_scheduler.CyclicLRWithRestarts(optimizer=optimizer, batch_size=batch_size, epoch_size=sample_count, restart_period=restart_period, t_mult=restart_mult, policy="cosine", verbose=True)
    loss_function = torch.nn.MSELoss()

    error_train = np.zeros(epochs)

    I = np.arange(sample_count)
    for epoch in range(epochs):
        scheduler.step()
        np.random.shuffle(I)
        error = 0.0
        for i in range(0, sample_count, batch_size):
            print('Progress', round(100 * i / sample_count, 2), "%", end="\r")
            train_indices = I[i:i+batch_size]

            xBatch = utility.ToDevice(torch.from_numpy(utility.ReadChunk(InputFile, pointersX[train_indices])))
            yBatch = utility.ToDevice(torch.from_numpy(utility.ReadChunk(OutputFile, pointersY[train_indices])))
            # xBatch = utility.ToDevice(torch.from_numpy(InputFile[train_indices]))
            # yBatch = utility.ToDevice(torch.from_numpy(OutputFile[train_indices]))

            yPred, gPred, w0, w1, w2 = network(xBatch)

            loss = loss_function(utility.Normalize(yPred, network.Ynorm), utility.Normalize(yBatch, network.Ynorm))
            optimizer.zero_grad()
            loss.backward()
            optimizer.step()
            scheduler.batch_step()

            error += loss.item()
    
        utility.SaveONNX(
            path=save+'/'+str(epoch+1)+'.onnx',
            model=network,
            input_size=input_dim,
            input_names=['X'],
            output_names=['Y', 'G', 'W0', 'W1','W2']
        )
        print('Epoch', epoch+1, error/(sample_count/batch_size))
        error_train[epoch] = error/(sample_count/batch_size)
        error_train.tofile(save+"/error_train.bin")

class Model(torch.nn.Module):
    def __init__(self, gating_indices, gating_input, gating_hidden, gating_output, main_indices, main_input, main_hidden, main_output, dropout, input_norm, output_norm):
        super(Model, self).__init__()

        if len(gating_indices) + len(main_indices) != len(input_norm[0]):
            print("Warning: Number of gating features (" + str(len(gating_indices)) + ") and main features (" + str(len(main_indices)) + ") are not the same as input features (" + str(len(input_norm[0])) + ").")

        self.gating_indices = gating_indices
        self.main_indices = main_indices

        self.GW1 = self.weights([gating_hidden, gating_input])
        self.Gb1 = self.bias([gating_hidden, 1])

        self.GW2 = self.weights([gating_hidden, gating_hidden])
        self.Gb2 = self.bias([gating_hidden, 1])

        self.GW3 = self.weights([gating_output, gating_hidden])
        self.Gb3 = self.bias([gating_output, 1])

        self.EW1 = self.weights([gating_output, main_hidden, main_input])
        self.Eb1 = self.bias([gating_output, main_hidden, 1])

        self.EW2 = self.weights([gating_output, main_hidden, main_hidden])
        self.Eb2 = self.bias([gating_output, main_hidden, 1])

        self.EW3 = self.weights([gating_output, main_output, main_hidden])
        self.Eb3 = self.bias([gating_output, main_output, 1])

        self.dropout = dropout
        self.Xnorm = Parameter(torch.from_numpy(input_norm), requires_grad=False)
        self.Ynorm = Parameter(torch.from_numpy(output_norm), requires_grad=False)

    def weights(self, shape):
        alpha_bound = np.sqrt(6.0 / np.prod(shape[-2:]))
        alpha = np.asarray(np.random.uniform(low=-alpha_bound, high=alpha_bound, size=shape), dtype=np.float32)
        return Parameter(torch.from_numpy(alpha), requires_grad=True)

    def bias(self, shape):
        return Parameter(torch.zeros(shape, dtype=torch.float), requires_grad=True)

    def blend(self, g, m):
        a = m.unsqueeze(1)
        a = a.repeat(1, g.shape[1], 1, 1)
        w = g.reshape(g.shape[0], g.shape[1], 1, 1)
        r = w * a
        r = torch.sum(r, dim=0)
        return r

    def forward(self, x):
        x = utility.Normalize(x, self.Xnorm)

        #Gating
        g = x[:, self.gating_indices]
        g = g.transpose(0,1)

        g = F.dropout(g, self.dropout, training=self.training)
        g = F.elu(self.GW1.matmul(g) + self.Gb1)

        g = F.dropout(g, self.dropout, training=self.training)
        g = F.elu(self.GW2.matmul(g) + self.Gb2)

        g = F.dropout(g, self.dropout, training=self.training)
        g = F.softmax(self.GW3.matmul(g) + self.Gb3, dim=0)

        #Main
        m = x[:, self.main_indices]
        m = m.reshape(m.shape[0], m.shape[1], 1)

        m = F.dropout(m, self.dropout, training=self.training)
        w0 = self.blend(g, self.EW1)
        m = F.elu(w0.matmul(m) + self.blend(g, self.Eb1))

        m = F.dropout(m, self.dropout, training=self.training)
        w1 = self.blend(g, self.EW2)
        m = F.elu(w1.matmul(m) + self.blend(g, self.Eb2))
        
        
        m = F.dropout(m, self.dropout, training=self.training)
        w2 = self.blend(g, self.EW3)
        m = w2.matmul(m) + self.blend(g, self.Eb3)
        
        m = m.reshape(m.shape[0], m.shape[1])

        return utility.Renormalize(m, self.Ynorm), g, w0, w1, w2