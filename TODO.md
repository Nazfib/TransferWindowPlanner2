# To-do list

## General

- [ ] Find a better name than TWP2
- [ ] Move this to-do list to GitHub issues
- [x] Depend on MechJebLib directly, instead of including some files from it

## GUI

- [ ] Drag-click to zoom into a launch window
- [x] Persistence: remember input settings and plot between scene switches
- [ ] Add a way to restore a plot from a KAC/Alarm Clock alarm?
- [ ] Add localization support to all strings
- [x] Add Click-Through-Blocker support
- [ ] Make KAC alarm margin configurable

## Maths

- [ ] Disallow setting a departure or arrival time after the next SOI crossing
  of the origin or target
  - This should the `Orbit.EndUT` field; but does it work for un-focussed
    vessels?

## Principia

- [ ] ~~Try using the `CelestialGetPosition` API from Principia.~~ Update:
  using an infinite SoI makes the Δv be exactly right (at least for the case
  where the launch window is right now), so this doesn't seem to be needed. Can
  revisit if longer-term predictions still turn out to be wrong.
- [ ] Check how the reference frames between Principia, Unity and KSP line up
  - Nathan has found a way to correctly find the inclination, and implemented
    it in ROUtils. See about either copying it, or adding a dependency (if
    there's more useful stuff in there)

