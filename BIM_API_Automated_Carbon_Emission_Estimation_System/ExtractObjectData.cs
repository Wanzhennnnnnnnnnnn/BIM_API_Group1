using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Windows; // 引用 WPF

namespace BIM_API_Automated_Carbon_Emission_Estimation_System
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExtractObjectData : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;

            try
            {
                // 1. 建立新的 WPF 視窗
                //    (SelectionWindow 來自 SelectionWindow.xaml.cs)
                SelectionWindow selectionWindow = new SelectionWindow(doc);

                // 2. 顯示視窗
                selectionWindow.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = "開啟擷取視窗時發生錯誤: " + ex.Message;
                return Result.Failed;
            }
        }
    }
}
