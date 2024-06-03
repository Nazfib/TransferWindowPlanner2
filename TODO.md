# To-do list

## General

- [ ] Find a better name than TWP2
- [x] Depend on MechJebLib directly, instead of including some files from it

## GUI

- [ ] Drag-click to zoom into a launch window
- [x] Persistence: remember input settings and plot between scene switches
- [ ] Add a way to restore a plot from a KAC/Alarm Clock alarm?
- [ ] Add localization support to all strings
- [x] Add Click-Through-Blocker support
- [x] Make KAC alarm margin configurable
- [x] Plot margin: Make limits configurable for both departure and arrival,
  separately. A linear limit, default ~1000 m/s, would probably be appropriate.
- [ ] Investigate: is setting time-of-flight better than arrival date? By a
  large margin, most of the Lambert calculation results are completely ignored!

## Maths

- [ ] Disallow setting a departure or arrival time after the next SOI crossing
  of the origin or target
  - This should the `Orbit.EndUT` field; but does it work for un-focussed
    vessels?

## Principia

- [ ] Fix the inclination and LAN calculations when using Principia. In
  particular, when not in the SoI of the departure planet, the ejection LAN and
  inclination are wrong; when not in the SoI of the arrival planet, the arrival
  inclination is wrong. Same goes for the RA and DEC of the asymptote; they are
  relative to the reference frame of the current SoI, not that of the relevant
  planet. When planning from the VAB, in the space center, or on the launch
  pad, the values are correct for Earth (Kerbin) — this is the most common
  case.
  - Nathan has found a way to correctly find the inclination, and implemented
    it in ROUtils. See about either copying it, or adding a dependency (if
    there's more useful stuff in there)

