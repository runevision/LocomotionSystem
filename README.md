# Locomotion System
__Version 1.0.3 - for Unity 2019.4 or higher__

_Grounding your walking and running animations, in any direction, on any terrain._

By _Rune Skovbo Johansen_  
[runevision.com](http://runevision.com)  
In collaboration with _Unity Technologies ApS_  
[unity.com](https://unity.com)

Based on the Master's Thesis [Automated Semi-Procedural Animation for Character Locomotion](http://runevision.com/thesis/) by Rune Skovbo Johansen.


## Introduction

The Locomotion System for Unity automatically blends your keyframed or motion-captured walk and run cycles and then adjusts the movements of the bones in the legs to ensure that the feet step correctly on the ground. The system can adjust animations made for a specific speed and direction on a plain surface to any speed, direction, and curvature, on any surface, including arbitrary steps and slopes.

The Locomotion System can be used with characters with a skinned skeleton and some animations. The character must have at least one leg, and each leg must have at least two segments. The character must also have at least one idle animation (it can be just one frame) and at least one walk or run cycle animation.

The Locomotion System does not enforce any high level control scheme but rather lets you move your character around by any means you desire. The Locomotion System silently observes the position, alignment, velocity and rotational velocity of your character and deduces everything from that, along with some raycasts onto the ground.

This flexibility means that you can use a `Character Controller`, a `Rigid Body`, or something else entirely to move your character around in the world, exactly as you would normally do.

The Locomotion System is _not_:

- A physics-based system or active animated ragdoll system.
- The system has no integration with the physics simulation. It is purely kinematic, though it does base the kinematics on some raycasts onto the geometry of the ground.
- A behavior-based system.
- The system cannot make the character react instinctively to external forces such as being punched, tripping and falling, or being shot. The system only blends and slightly adjusts your existing animations.
- A unified system that can be used for all animation of a character.
- The system only controls the legs. (The whole body is typically implicitly controlled since the system blends together multiple full body animations. However, this can be overridden by the user with specific animations for the upper body, using the usual means available in Unity.) 

For the Locomotion System to work, you only need the files in the folder `Assets / Locomotion System Files / Locomotion System`. All other files are not directly part of the Locomotion System and do not have to be included in your project. However, the Quick Start Guide in the documentation uses additional files from the project folder.


## Documentation

For information on how to use the Locomotion System, [see the documentation](Assets/Locomotion%20System%20Files/Documentation/documentation.html).


## License

All code files in this project are Copyright (c) 2008-2020, Rune Skovbo Johansen. They are licensed according to the [Mozilla Public License Version 2.0](LICENSE).

Non-code asset files are Copyright (c) 2008, Rune Skovbo Johansen & Unity Technologies ApS. They are licensed according to the [TUTORIAL ASSETS LICENSE AGREEMENT](TUTORIAL%20ASSETS%20LICENSE%20AGREEMENT.rtf).
