using System;
using Autodesk.Revit.UI;

namespace AutoPileCap.Handlers
{
    public class GenericEventHandler : IExternalEventHandler
    {
        public Action<UIApplication> Action { get; set; }

        public void Execute(UIApplication app)
        {
            Action?.Invoke(app);
        }

        public string GetName()
        {
            return "Generic Event Handler";
        }
    }
}
