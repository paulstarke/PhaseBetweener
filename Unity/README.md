# Motion In-betweening with Phase Manifolds

This repository contains the code for the phase-based motion in-betweening technology.
Any questions, feel free to ask. For any issues you might find, please let me know and send me a message; paulstarke.ps@gmail.com.

## Getting Started

1. Download the learned phases and processed [Assets](https://starke-consult.de/PhaseBetweener/MotionCapture.zip) of the LaFan1 motion capture dataset.

2. Extract  `MotionCapture.zip`  to `Assets/Demo/Authoring` folder.

How to play the demos?
You can launch multiple demos in Unity itself by opening the scene files at Unity
-> Assets -> Demo -> Thesis -> AuthoringDemo.unity, BipedDemo.unity, and DogDemo.unity.
The system should run when hitting Play. If not, ensure that Unity Version 2020.3.18f1 and the Barracuda 2.0.0 package (required for ONNX inference) is installed.

Two runtime controllers (InBetweeningController.cs and AuthoringInBetweeningController.cs) are implemented for the task.
For both controllers, select if the system should run with no phases, local motion phases or learned (deep) phases by adjusting the corresponding
parameters in the Inspector UI. The trained models for each option are available in -> Thesis -> Model.
For the LaFAN1 (biped) character it is recommended to use trained models with "DeepPhase" included in their names.
Note that, switching between quadruped and biped characters/models requires to change the topology parameters in the controller.
Visualization options can be turned on/off in the inspector.

InBetweeningController.cs samples the control parameters from a processed animation clip asset in Unity.
This clip can be changed in the Inspector field "Asset" and the desired in-betweening time can be set through "Sampling Offset".

AuthoringInBetweeningController.cs is using the control parameters from the linked Authoring tool.

How to use the Authoring Tool?
To create sparse keyframe for the character, add the "Authoring.cs" script to any gameobject in your scene. 
===Controls===
Add/Insert/Delete controlpoints: <Ctrl> + <LeftMouseClick> in Scene View
Select-Mode: <LeftMouseClick> on a controlpoint in the Scene View -> properties of this control point show up in inspector
Unselect: <Esc> in Scene View
Undo: <Ctrl> + Z
Redo: <Ctrl> + Y
==============
Drag and Drop existing control points in scene view to change their position. Translate or rotate the gameobject to move the whole spline path correspondingly.
Each control point must have one target pose. To load poses of the character from the mocap, import the processed mocap assets in the <Motion Import Options> Menue in the inspector.
To change the target pose of a controlpoint, press <Sync Assets> in the <Betweening Module> Inspector and select desired frame. Rotate the pose by dragging the circle near the controlpoint.
Once the Authoring is set up, simply attach it to the AuthoringInBetweeningController script of the character. 

How to reproduce the results?
The complete code that was used for processing, training, and generating the in-between movements is provided in this repository.
The original motion data is available here:
https://github.com/ubisoft/ubisoft-laforge-animation-dataset (Biped, LaFAN1)
http://www.starke-consult.de/AI4Animation/SIGGRAPH_2018/MotionCapture.zip (Quadruped, Dog)

A complete walkthrough to reproduce the biped model is given below.

#1 Open the Motion Capture Scene, located in Unity -> Assets -> Demo -> Thesis -> MotionCapture -> Mocap_LaFan.unity.
#2 Click on the MotionEditor game object in the scene hierarchy window.
#3 Open the Motion Exporter (Header -> AI4Animation -> Exporter -> Motion Exporter). Set "Frame Shifts" to 0 and "Frame Buffer" to 30, "Phases" to "Deep Phases" and have the box for "Write Mirror" checked.
#4 Click the "Export" button, which will generate the training data and save it in the DeepLearning folder. Save this data into DeepLearning -> dataset -> LaFAN1_Full_DeepPhases folder.
#5 Navigate to the DeepLearning -> Models -> GNN folder.
#5 Run the InBetweeningNetwork.py file which will start the training.
#6 Wait for a few hours.
#7 You will find the trained .onnx model in the training folder.
#8 Import the model into Unity and link it to the controller.
#9 The system should run when hitting Play.

=========================================
Starting with the raw motion capture data.

If you decide to start from the raw motion capture and not use the already processed assets in Unity, you will need to download the LaFAN1 dataset and do the following steps:

#1 Import the motion data into Unity by opening the BVH Importer (Header -> AI4Animation -> Importer -> BVH Importer). Define the path where the original .bvh data is saved on your hard disk, and where the Unity assets shall be saved inside the project.
#2 Set Scale to 0.01 and press "Load Directory" and "Import Motion Data".
#3 Create a new scene, add an empty game object and add the MotionEditor component to it.
#4 Copy the path where the imported motion data assets have be saved and click "Import".
#5 In the "Editor Settings" at the bottom, make sure that "Target Framerate" is set to 30Hz.
#6 Open the MotionProcessor window (Header -> AI4Animation -> Tools -> MotionProcessor), make sure that "LaFAN Pipeline" is selected and click "Process".
#7 Wait for a few hours.
#8 At this point, the raw motion capture data has been automatically processed and is at the same stage as the motion assets provided in this repository. You are ready to continue with the steps above to export the data, train the network and control the character movements.

The code to train the Periodic Autoencoder and extract the phase parameters for the mocap is available here:
https://github.com/sebastianstarke/AI4Animation/tree/master/AI4Animation/SIGGRAPH_2022/PyTorch

Any questions, feel free to ask. For any issues you might find, please let me know and send me a message to paulstarke.ps@gmail.com or +4917657627917.

