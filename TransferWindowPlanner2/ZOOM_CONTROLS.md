# Transfer Window Planner 2 - Graph zoom

After generating a plot:

- **Left-click** a point on the porkchop plot to select a transfer (unchanged).
- **Left-drag** across the plot to draw a highlighted rectangle; release to recompute the plot for that portion of the graph. The horizontal range is **departure time** and the vertical range is **transfer duration**. Dragging in either direction works.
- **Zoom out** goes back one selection. **Reset zoom** goes back to the original plotted range.
- **Esc** or **right-click** while dragging cancels the rectangle. Very small drags are treated as clicks; drags too narrow in one direction are ignored.
- The earliest/latest date fields include time of day while zoomed so sub-day ranges remain visible and editable. The solver retains the precise selected times.
- **Plot it!** starts a fresh plot from the current input fields and clears the zoom history. **Reset times** restores the default date/flight-time inputs and clears zoom history; use **Plot it!** to update the graph.

Each zoom runs a new full-resolution solve (500 x 400), rather than scaling old pixels.

## Window dragging

- Dragging **outside the plot** still moves the TWP2 window using the original `GUI.DragWindow()` behavior. The zoom handler only intercepts a left mouse press that starts **inside** the drawn plot.
- Graph zoom no longer takes `GUIUtility.hotControl`. This prevents graph drag state from locking out other window mouse operations after a drag.

## Graph scales

- The **bottom** scale shows **departure dates**, increasing left to right. Date labels include the time of day when zoomed in to a range shorter than three KSP days.
- The **left** scale shows **elapsed transfer time in days**, decreasing from top to bottom. Precision adapts to the zoom level.
- Tick values come directly from the solver grid, so they follow each new plot, zoom in, Zoom out, and Reset zoom. The graph remains 500 x 400 pixels and only drags starting inside that image initiate zoom selection.
- Space for both scales is reserved in the window layout, keeping labels clear of the plotted data.

## Show/hide scales

- The **Show scales** checkbox is in the graph's top row, beside the Departure / Arrival / Total plot selector. It is enabled by default.
- Uncheck it to hide **both** the departure-date scale under the plot and the elapsed-time scale to its left. The unused left-axis margin and bottom-axis space are removed, and the window becomes narrower.
- Check it to restore both scales. The graph's data and full 500 x 400 resolution are unchanged, and zoom history/selected transfer are preserved.
- The checkbox is a display-only option: changing it does not recalculate the plot or modify the plotted ranges.


