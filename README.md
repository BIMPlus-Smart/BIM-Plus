# BIM Plus - Pile Cap Rebar for Autodesk Revit 2026

Professional structural reinforcement plugin built specifically for **Autodesk Revit 2026** (.NET 8.0 / C# / WPF).

---

## ⚡ Ribbon Location

- **Tab Name**: **`BIM Plus`**
- **Panel Name**: **`Pile Cap Rebar`**
- **Command**: ⚡ **`Pile Cap Rebar`**

---

## 🏗️ Key Features

1. **Strict Structural Foundation Detection**:
   - Automatically filters selections to **only** include Structural Foundations (`OST_StructuralFoundation`), ignoring columns, walls, beams, levels, and grids.
   - Includes an interactive **"👆 Pick Foundations"** button inside the dialog that strictly locks the Revit cursor to structural foundations (pile caps/footings) using `ISelectionFilter`.

2. **3D Reinforcement Cage Generator**:
   - **Bottom Mat**:
     - Direction 1 (Length / X) & Direction 2 (Width / Y)
     - Configurable bar diameter and spacing (mm)
     - 3D bent legs / U-bars (upward hooks) or straight bars
   - **Top Mat**:
     - Direction 1 & Direction 2 with downward hooks
   - **Side / Skin Ties (Perimeter Stirrups)**:
     - Closed perimeter stirrups spanning the depth of the pile cap
   - **Concrete Cover**:
     - Independent Bottom Cover (75 mm), Top Cover (50 mm), and Side Cover (50 mm)
   - **Visibility**:
     - Automatically renders rebars as **Unobscured** in 3D views.

---

## 🚀 How to Run in Revit 2026

1. **Restart Revit 2026** so it loads the updated assembly.
2. Open your model.
3. Look at the top ribbon for the **`BIM Plus`** tab.
4. Click ⚡ **`Pile Cap Rebar`**.
5. Select or click **"👆 Pick Foundations"** to choose your pile caps, configure your reinforcement, and click **⚡ Generate Rebar**!
