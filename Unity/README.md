# Motion In-betweening with Phase Manifolds

This repository contains the code for the phase-based motion in-betweening technology.
Any questions, feel free to ask. For any issues you might find, please let me know and send me a message; paulstarke.ps@gmail.com.

## Getting Started

1. Download the learned phases and processed [Assets](https://starke-consult.de/PhaseBetweener/MotionCapture.zip) of the LaFan1 motion capture dataset.

2. Extract  `MotionCapture.zip`  to `Assets/Demo/Authoring` folder.

## How to play the demos?
We provide two demo scenes in `Assets/Demo/Authoring`. Open them in Unity and hit play - the system should run automatically.
If not, ensure that Unity Version 2020.3.18f1 and the Barracuda 2.0.0 package (required for ONNX inference) is installed. <br>
Two runtime controllers are implemented for the task. <br>
`InBetweeningController.cs` samples the control parameters from a processed animation clip asset in Unity. <br>
`AuthoringInBetweeningController.cs` is using the control parameters from the linked Authoring tool. <br>
For both controllers, select if the system should run with no phases, local motion phases or learned (deep) phases by adjusting the corresponding
parameters in the Inspector UI. The trained models for each option are available in `Demo/Authoring/Model`.
Visualization options can be turned on/off in the inspector.

## How to use the Authoring Tool?
To create sparse keyframes for the character, add the `Authoring.cs` script to any gameobject in your scene. <br>

### Controls
- Add/Insert/Delete controlpoints: `<Ctrl> + LeftMouseClick` in Scene View <br>
- Select-Mode: `LeftMouseClick` on a controlpoint in the Scene View -> properties of this control point show up in inspector <br>
- Unselect: `<Esc>` in Scene View <br>
- Undo: `<Ctrl> + Z` <br>
- Redo: `<Ctrl> + Y` <br>

Drag and drop existing control points in scene view to change their position. Translate or rotate the gameobject to move the whole spline path correspondingly.
Each control point must have a target pose. To load poses of the character from the motion capture, import the processed assets in the `<Motion Import Options>` menue in the inspector.
To change the target pose of a controlpoint, press `<Sync Assets>` in the `<Betweening Module>` inspector menue and select a desired frame. Rotate the target pose by dragging the circle near the controlpoint with your mouse.
Once the Authoring is set up, make sure to link it to the `AuthoringInBetweeningController.cs` script of the character. 

## How to reproduce the results?
The complete code that was used for processing, training, and generating the in-between movements is provided in this repository.
To reproduce the model complete the following steps:

1. Open `Assets/Demo/Authoring/MotionCapture/Mocap_LaFan.unity`. <br>
2. Click on the MotionEditor game object in the scene hierarchy window. <br>
3. Open the Motion Exporter `Header -> AI4Animation -> Exporter -> Motion Exporter`. Set "Frame Shifts" to 0 and "Frame Buffer" to 30, "Phases" to "Deep Phases" and have the box for "Write Mirror" checked. <br>
4. Click the "Export" button, which will generate the training data and save it. <br>
5. Navigate to `DeepLearningONNX/Models/GNN`. <br>
6. Run `InBetweeningNetwork.py` which will start the training. <br>
7. Wait for a few hours. <br> 
8. You will find the trained .onnx model in the training folder. <br>
9. Import the model into Unity and link it to the controller. <br>
10. Hit Play.

----
If you decide to start from the raw motion capture and not use the already processed assets in Unity, you will need to download the [LaFAN1](https://github.com/ubisoft/ubisoft-laforge-animation-dataset) dataset and complete the  following steps:

1. Import the motion data into Unity by opening the BVH Importer `Header -> AI4Animation -> Importer -> BVH Importer`. Define the path where the original .bvh data is saved on your hard disk, and where the Unity assets should be saved inside the project.
2. Set Scale to 0.01 and press "Load Directory" and "Import Motion Data".
3. Create a new scene, add an empty game object and add the MotionEditor component to it.
4. Copy the path where the imported motion data assets have be saved and click "Import".
5. In the "Editor Settings" at the bottom, make sure that "Target Framerate" is set to 30Hz.
6. Open the MotionProcessor window `Header -> AI4Animation -> Tools -> MotionProcessor`, make sure that "LaFAN Pipeline" is selected and click "Process".
7. Wait for a few hours.
8. At this point, the raw motion capture data has been automatically processed and is at the same stage as the motion assets provided in this repository. You are ready to continue with the steps above to export the data, train the network and control the character movements.

The code to train the Periodic Autoencoder and extract the phase parameters for the mocap is available [here](https://github.com/sebastianstarke/AI4Animation/tree/master/AI4Animation/SIGGRAPH_2022/PyTorch).



