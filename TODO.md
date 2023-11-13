# To-do list

## General

- [ ] Find a better name than TWP2
- [ ] Move this to-do list to GitHub issues

## GUI

- [ ] Drag-click to zoom into a launch window
- [ ] Persistence: remember input settings and plot between scene switches
  - Add a way to restore a plot from a KAC/Alarm Clock alarm?
- [ ] Add localization support to all strings
- [x] Add Click-Through-Blocker support
- [ ] Make KAC alarm margin configurable

## Maths

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
  - Nathan has found a way to correctly find the inclination, and implemented
    it in ROUtils. See about either copying it, or adding a dependency (if
    there's more useful stuff in there)

