# CharacterController2D
English | [中文](README_ch.md)

## Introduction

If you have attempted to control your character using Rigidbody2D in Unity 2D and tried to have it moving on slopes, you may find that it doesn't work and can get tricky to fix, at least for me. This repository is my solution to this problem, inspired by `CharacterController` (though I haven't used `CharacterController` before). Basically, it's built around my own needs, but if you're banging your head against the slopes in Unity 2D like I was, you may find this helpful too.


## Usage
I intend to make it simple to use.

Although I implemented the collisions manually, it still requires a `BoxCollider2D` to indicate the rough size of the character. Many underlying values are adjusted proportionally to the size of the `BosCollider2D`. Additionally, you need to implement gravity by yourself.


### `platformMask`
Select the ground layer of your scene then the character will only collide with the ground.

### `verticalCheckDistance` and `horizontalCheckDistance`
I implement collision detection using rays. `verticalCheckDistance` and `horizontalCheckDistance` is the length of the rays in the two directions.

### Ray Distance
This is the distance between the rays. It can be interpreted as the width of the collider

#### `horizontalRayDistance`
This is the distance between the horizontal rays. It determines the collider's width in the horizontal direction.

#### `specialRayDistance`
When the character is climbing a slope, it is undesirable to have it standing on one of its vertices.

The smaller this value is, the narrower the vertical collider is, and the character's feet will be better fitted into the slope.

It also determines the wideness of the collider when the character is moving upwards, so when this value is smaller, the character is less likely to get stuck by objects over their head.

#### `verticalRayDistance`
This is the distance between the vertical rays. It determines the collider's width in the vertical direction.

However, given the aforementioned scenarios, this value comes into play almost only when the character is landing from a fall.

The larger this value is, the easier the character lands on platforms.

### `collisionHardness`
As the ray distances in the vertical and horizontal directions can be lowered, it's possible for the character to stuck into walls. This value controls the speed at which the character gets expelled from walls.

The smaller this value is, the slower the character gets expelled from walls, but the smoother the character navigates across various terrains.

### `gripDegree`
In a nutshell, this is the downward speed of the character when standing on the ground. It may sound a bit weird, but it's my way to keep the character on the ground. However, a large value may cause the character to suddenly plunge when leaving a platform.

## Limitations
As this project is built around my needs, some minor glitches that have no impact on my use are left unfixed. For instance (if I remembered correctly), if the `horizontalRayDistance` is too large and the `specialRayDistance` is too small, the character would have some problem climbing slopes.

## Acknowledgement
I would like to thank Bitool for teaching me how to organize my code. It is with his help that my code became readable.