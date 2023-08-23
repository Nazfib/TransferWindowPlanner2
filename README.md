# Transfer Window Planner 2

This is a completely new transfer window planning mod for KSP. It is heavily
inspired by TriggerAu's original TWP.

![Screenshot of the in-game transfer planning window](/artwork/twp2-preview.png)

# TODO

## General

- [ ] Find a better name than TWP2

## GUI

- [ ] Drag-click to zoom into a launch window
- [ ] Persistence: remember input settings and plot between scene switches
  - Add a way to restore a plot from a KAC/Alarm Clock alarm?
- [ ] Add localization support to all strings
- [ ] Add Click-Through-Blocker support

## Maths

- [ ] Check how hyperbolic starting/ending orbits are handled
  - Mostly relevant for rendez-vous with comets on a hyperbolic trajectory
  - Check if the math works correctly (mostly MJL's state vector calculation)
- [ ] Disallow setting a departure or arrival time after the next SOI crossing
  - This should the `Orbit.EndUT` field; but does it work for un-focussed
    vessels?

## Accuracy improvements when using Principia

Investigate why burns with Principia always take ~50 to 100 m/s more than TWP
predicts. My main hypothesis is that the initial/final positions of the planets
are too inaccurate (based on the osculating orbit) to work properly; especially
the Earth, which has a big Moon, might have a slight variation in it's velocity
over the course of a month.

- [ ] Try using the `CelestialGetPosition` API from Principia. 
- [ ] Check how the reference frames between Principia, Unity and KSP line up
  - For example, the "arrival asymptote direction" angles are very wrong;
    probably, when creating a plot when the current central body is _not_ Earth
    will cause inclination/lan/asymptote angles to be incorrect as well.

# License

Licensed under the MIT license (see [LICENSE](/LICENSE)). 

This mod is heavily inspired by TriggerAu's original TransferWindowPlanner. It
also uses a modified version of the marker texture. TWP is used under the MIT
license (see [LICENSE.TransferWindowPlanner](/LICENSE.TransferWindowPlanner)).

Uses code from MechJebLib, a part of the MechJeb2 mod for KSP. MechJebLib is
used under the terms of the MIT license (see
[LICENSE.MechJebLib](/LICENSE.MechJebLib)).

MechJeb2 is included as a git submodule, because MechJebLib is not available as
a separate repo. MechJeb2 is licensed under the [GNU General Public License, 
Version 3](https://github.com/MuMech/MechJeb2/blob/fcc1b4c0e044244fa3f7fec0efb37127d9bae59d/LICENSE.md).
TWP2 uses no code from MechJeb other than that in MechJebLib.

The color map used in the plot is CET-L20 by Peter Kosevi. It is available at
[colorcet.com](https://colorcet.com) under a CC-BY-4.0 license.

