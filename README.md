**Motion In-Betweening with Phase Manifolds**<br >
*In 22nd ACM SIGGRAPH/EUROGRAPHICS Symposium on Computer Animation (SCA 2023)*<br >
<sub>
<a href="https://www.linkedin.com/in/paul-starke-0787211b4/">Paul Starke</a>, 
<a href="https://www.linkedin.com/in/sebastian-starke-b281a6148/">Sebastian Starke</a>, 
<a href="https://www.linkedin.com/in/taku-komura-571b32b/">Taku Komura</a>, 
<a href="https://www.linkedin.com/in/frank-steinicke-b239639/">Frank Steinicke</a>
<sub>
------------
<img src ="Media/Teaser.png" width="100%">

<p align="center">
This work introduces a novel data-driven motion in-betweening system to reach target poses of characters by making use of phases variables learned by a Periodic Autoencoder. The approach utilizes a mixture-of-experts neural network model, in which the phases cluster movements in both space and time with different expert weights. Each generated set of weights then produces a sequence of poses in an autoregressive manner between the current and target state of the character. In addition, to satisfy poses which are manually modified by the animators or where certain end effectors serve as constraints to be reached by the animation, a learned bi-directional control scheme is implemented to satisfy such constraints. Using phases for motion in-betweening tasks sharpen the interpolated movements, and furthermore stabilizes the learning process. Moreover, more challenging movements beyond locomotion behaviors can be synthesized. Additionally, style control is enabled between given target keyframes. The framework can compete with state-of-the-art methods for motion in-betweening in terms of motion quality and generalization, especially in the existence of long transition durations. This framework contributes to faster prototyping workflows for creating animated character sequences, which is of enormous interest for the game and film industry.
</p>

<p align="center">
-
<a href="https://youtu.be/MbS1YcKhyyA">Video</a>
-
<a href="Media/paper.pdf">Paper</a>
-
<a href="Unity/">Code, Demo & Tool</a>
-
<a href="Unity/README.md">ReadMe</a>

<p align="center">
    <a href="https://youtu.be/MbS1YcKhyyA">
    <img src="Media/Thumbnail.png">
    </a>
</p>

</p>
<p align="center">
<img src ="Media/AuthoringTool.png" width="60%" height="30%">
</p>

Copyright Information
============
This project is only for research or education purposes, and not freely available for commercial use or redistribution. The motion capture data and 3D character model from [ubisoft-laforge](https://github.com/ubisoft/ubisoft-laforge-animation-dataset) is available only under the terms of the [Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License](https://github.com/ubisoft/ubisoft-laforge-animation-dataset/blob/master/license.txt).
