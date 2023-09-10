# Character Motor Experiment

## Resources

- [Bunnyhopping from the Programmer's Perspective](http://flafla2.github.io/2015/02/14/bunnyhop.html)
- [Q3A - Player Movement Source Code](https://github.com/id-Software/Quake-III-Arena/blob/master/code/game/bg_pmove.c#L240)
  - Consider [ioq3](https://github.com/ioquake/ioq3) if attempting to compile on a modern platform.
- [Half Life 1 SDK](https://github.com/ValveSoftware/halflife/blob/master/pm_shared/pm_shared.c#L794)
- [Source SDK 2013 - Source Code](https://github.com/ValveSoftware/source-sdk-2013/blob/56accfdb9c4abd32ae1dc26b2e4cc87898cf4dc1/sp/src/game/shared/gamemovement.cpp#L1822)
- [Super Character Controller](https://roystanross.wordpress.com/category/unity-character-controller-series/)
- [2D Platformer Collision Detection](http://deranged-hermit.blogspot.com/2014/01/2d-platformer-collision-detection-with.html)

## Roadmap

- [x] Support for stairs
- [ ] Easier collision detection and handling
- [ ] Events / callbacks for changes in grounding status
- [ ] Accessors for estimated velocity
- [ ] Accessors for foot position
- [ ] Basic push/stand force
- [ ] Accessors for character height
- [ ] Methods for applying forces/pushes to character
- [ ] Create a hazard course
- [ ] Create a more complete playground
- [ ] Method for teleportation
- [ ] Major refactor to use a cast/sweep-based test instead of move-and-check approach
- [ ] Refactor to generic-purpose Kinematic Body
- [ ] Refactor to support different collision shapes

### Extended Feature Set

- [ ] Ladders
- [ ] Swimming
- [ ] Moving with Moving Platforms

### Related Projects
- https://github.com/dbrizov/NaughtyCharacter - Kinematic, Wraps Character Controller component
- https://github.com/nicholas-maltbie/OpenKCC - Kinematic, Custom
- https://github.com/aleksandrpp/CompetitiveController - Kinematic, Custom
- https://github.com/joebinns/stylised-character-controller - Simulated, Rigidbody
- [Kinematic Character Controller by Philippe St-Amand](https://assetstore.unity.com/packages/tools/physics/kinematic-character-controller-99131) - Kinematic, Custom
- [Character Controller package by Unity Technologies](https://docs.unity3d.com/Packages/com.unity.charactercontroller@1.0/manual/index.html) - Kinematic, Custom, DOTS
    - Possibly based off of Rival, a DOTS-based Unity Asset Store package, prior to Philippe St-Amand joining Unity
- Starter Assets for [First Person][unityFPS] and [Third Person][unityTPS] Character Controllers by Unity Technologies

[unityFPS]:https://assetstore.unity.com/packages/essentials/starter-assets-first-person-character-controller-urp-196525#content
[unityTPS]:https://assetstore.unity.com/packages/essentials/starter-assets-third-person-character-controller-urp-196526#content

#### Tutorials
- [Making A Physics Based Character Controller In Unity (for Very Very Valet)](https://www.youtube.com/watch?v=qdskE8PJy6Q&ab_channel=ToyfulGames) - Simulated, Rigidbody
- [Catlike Coding by Jasper Flick](https://catlikecoding.com/unity/tutorials/movement/) - Simulated, Rigidbody
- [Indie Wafflus' Genshin Impact Movement Tutorials](https://www.youtube.com/playlist?list=PL0yxB6cCkoWKuPoh_9dSvdItQENVx7YTW) - Simulated, Rigidbody
- [Unity DOTS Character Controller](https://www.vertexfragment.com/ramblings/unity-dots-character-controller/) - Kinematic, Custom, DOTS


## License

MIT License (c) 2019-2022 Terry Nguyen
