# BIM Plus - Structural Rebar Plugin for Autodesk Revit 2026 & 2027

Professional structural reinforcement plugin built for **Autodesk Revit 2026** (.NET 8.0) and **Autodesk Revit 2027** (.NET 10.0) using C# and WPF.

---

## ⚡ Ribbon Location

- **Tab Name**: **`BIM Plus⁺`**
- **Panels**:
  - `Pile Cap Rebar`: Pile Cap Rebar, Auto Section Box
  - `Columns`: Column Rebar (Rectangular, Circular, L/T-Shaped)
  - `Walls`: Wall Rebar
  - `Slabs`: Slab Rebar
  - `Beams`: Beam Rebar
  - `Stairs`: Stair Rebar
  - `Partitions`: Partition Rebar Manager

---

## 🏗️ Key Features

1. **Strict Structural Foundation Detection**:
   - Automatically filters selections to **only** include Structural Foundations (`OST_StructuralFoundation`), ignoring columns, walls, beams, levels, and grids.
   - Includes interactive **"👆 Pick Elements"** buttons that strictly lock the Revit cursor to structural elements using `ISelectionFilter`.

2. **3D Reinforcement Cage Generators**:
   - **Pile Caps**: Bottom & top mats, hook legs, side ties/stirrups, independent covers.
   - **Columns**: Main longitudinal bars, lap splices, crank options, tie zones.
   - **Beams & Walls**: Full boundary cages, stirrups, skin reinforcement.
   - **Slabs & Stairs**: Top & bottom mats, distribution bars, scissor joint reinforcement.
   - **Visibility**: Automatically renders rebars as **Unobscured** in 3D views.

3. **Multi-Targeting Architecture**:
   - **Revit 2026**: Built with `net8.0-windows` targeting Revit 2026 API.
   - **Revit 2027**: Built with `net10.0-windows` targeting Revit 2027 API (`BarTerminationsData` & `RebarTerminationOrientation`).

---

## 🚀 How to Run in Revit 2026 or 2027

1. Build or deploy using `build_and_deploy.bat` or run installer from `Output\BIM Plus⁺_Setup.exe`.
2. Start **Autodesk Revit 2026** or **Autodesk Revit 2027** (or run `run_revit.bat`).
3. Look at the top ribbon for the **`BIM Plus⁺`** tab.
4. Enjoy designing and generating 3D rebar!
