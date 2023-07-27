import sys
sys.path.append("../../../DeepLearningONNX")

import Library.Utility as utility
import Library.AdamWR.adamw as adamw
import Library.AdamWR.cyclic_scheduler as cyclic_scheduler
import Models.MLP.Network as this

import numpy as np
import torch
from torch.nn.parameter import Parameter
import torch.nn.functional as F

if __name__ == '__main__':
    load = "../../../DeepLearningONNX"
    save = "./Training"

    InputFile = load + "/Input.txt"
    OutputFile = load + "/Output.txt"
    Xnorm = utility.ReadNorm(load + "/InputNorm.txt")
    Ynorm = utility.ReadNorm(load + "/OutputNorm.txt")

    seed = 23456
    rng = np.random.RandomState(seed)
    torch.manual_seed(seed)
    
    epochs = 150
    batch_size = 32
    dropout = 0.3

    learning_rate = 1e-4
    weight_decay = 1e-4
    restart_period = 10
    restart_mult = 2

    print("Started creating data pointers...")
    pointersX = utility.CollectPointers(InputFile)
    pointersY = utility.CollectPointers(OutputFile)
    print("Finished creating data pointers.")

    sample_count = pointersX.shape[0]
    input_dim = Xnorm.shape[1]
    output_dim = Ynorm.shape[1]
    
    print(torch.__version__)

    layers = [input_dim, 512, 512, output_dim]
    activations = [F.elu, F.elu, None]

    print("Network Structure:", layers)

    network = this.Model(
        rng=rng,
        layers=layers,
        activations=activations,
        dropout=dropout,
        input_norm=Xnorm,
        output_norm=Ynorm
    )
    if torch.cuda.is_available():
        print('GPU found, training on GPU...')
        network = network.cuda()
    else:
        print('No GPU found, training on CPU...')
        
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

            yPred = network(xBatch)

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
            output_names=['Y']
        )
        print('Epoch', epoch+1, error/(sample_count/batch_size))
        error_train[epoch] = error/(sample_count/batch_size)
        error_train.tofile(save+"/error_train.bin")

class Model(torch.nn.Module):
    def __init__(self, rng, layers, activations, dropout, input_norm, output_norm):
        super(Model, self).__init__()

        self.rng = rng
        self.layers = layers
        self.activations = activations
        self.dropout = dropout
        self.Xnorm = Parameter(torch.from_numpy(input_norm), requires_grad=False)
        self.Ynorm = Parameter(torch.from_numpy(output_norm), requires_grad=False)
        self.W = torch.nn.ParameterList()
        self.b = torch.nn.ParameterList()
        for i in range(len(layers)-1):
            self.W.append(self.weights([self.layers[i], self.layers[i+1]]))
            self.b.append(self.bias([1, self.layers[i+1]]))

    def weights(self, shape):
        alpha_bound = np.sqrt(6.0 / np.prod(shape[-2:]))
        alpha = np.asarray(self.rng.uniform(low=-alpha_bound, high=alpha_bound, size=shape), dtype=np.float32)
        return Parameter(torch.from_numpy(alpha), requires_grad=True)

    def bias(self, shape):
        return Parameter(torch.zeros(shape, dtype=torch.float), requires_grad=True)

    def forward(self, x):
        x = utility.Normalize(x, self.Xnorm)
        y = x
        for i in range(len(self.layers)-1):
            y = F.dropout(y, self.dropout, training=self.training)
            y = y.matmul(self.W[i]) + self.b[i]
            if self.activations[i] != None:
                y = self.activations[i](y)
        return utility.Renormalize(y, self.Ynorm)
