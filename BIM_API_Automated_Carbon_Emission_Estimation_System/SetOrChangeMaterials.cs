using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIM_API_Automated_Carbon_Emission_Estimation_System
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class SetOrChangeMaterials  : IExternalCommand
    {
        // 根據 "BIM 碳排資料.docx" 檔案定義目標材質名稱
        // 混凝土使用 "預拌混凝土碳排" 的第一項 [cite: BIM 碳排資料.docx, Image 2]
        private const string _concreteMaterialName = "預拌混凝土(280kgf/cm2, 飛灰爐石替代率30%)";
        // 鋼筋使用 "鋼筋" 的第一項 [cite: BIM 碳排資料.docx, Image 3]
        private const string _rebarMaterialName = "鋼筋混凝土用鋼筋(SD280W)";

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            // 執行前的最後警告
            TaskDialog mainDialog = new TaskDialog("警告：即將修改模型");
            mainDialog.MainInstruction = "您確定要繼續嗎？";
            mainDialog.MainContent = "此操作將批次修改您專案中「所有」的柱、梁、牆、樓板和鋼筋的「類型」材質。\n\n" ;
            mainDialog.CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel;
            mainDialog.DefaultButton = TaskDialogResult.Cancel;

            if (mainDialog.Show() == TaskDialogResult.Cancel)
            {
                message = "操作已由使用者取消。";
                return Result.Cancelled;
            }

            try
            {
                // 啟動一個主事務 (Transaction) 來包覆所有變更
                using (Transaction tx = new Transaction(doc, "應用碳排資料庫材質"))
                {
                    tx.Start();

                    // --- 步驟 1: 取得或建立材質 ---
                    // 呼叫輔助方法來確保材質存在於專案中
                    ElementId concreteMatId = GetOrCreateMaterial(doc, _concreteMaterialName);
                    ElementId rebarMatId = GetOrCreateMaterial(doc, _rebarMaterialName);

                    if (concreteMatId == ElementId.InvalidElementId || rebarMatId == ElementId.InvalidElementId)
                    {
                        message = "無法找到或建立必要的材質。";
                        tx.RollBack();
                        return Result.Failed;
                    }

                    // --- 步驟 2: 應用材質到元件族群 (柱、梁) ---
                    List<BuiltInCategory> componentCategories = new List<BuiltInCategory>
                    {
                        BuiltInCategory.OST_StructuralColumns, // 結構柱
                        BuiltInCategory.OST_StructuralFraming  // 結構框架 (梁)
                    };
                    ApplyMaterialToComponentTypes(doc, concreteMatId, componentCategories, BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);

                    // --- 步驟 3: 應用材質到系統族群 (牆、樓板) ---
                    ApplyMaterialToSystemTypes(doc, concreteMatId, BuiltInCategory.OST_Walls);  // 牆
                    ApplyMaterialToSystemTypes(doc, concreteMatId, BuiltInCategory.OST_Floors); // 樓板

                    // --- 步驟 4: 應用材質到鋼筋 (Rebar) ---
                    ApplyMaterialToRebarTypes(doc, rebarMatId);

                    tx.Commit();
                }

                TaskDialog.Show("成功", $"已成功將材質應用到 柱、梁、牆、樓板和鋼筋 類型。\n\n混凝土: {_concreteMaterialName}\n鋼筋: {_rebarMaterialName}");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = "發生未預期的錯誤: " + ex.Message;
                return Result.Failed;
            }
        }

        /// <summary>
        /// 輔助方法：檢查材質是否存在，若否，則建立它。
        /// </summary>
        /// <returns>材質的 ElementId</returns>
        private ElementId GetOrCreateMaterial(Document doc, string materialName)
        {
            // 1. 嘗試尋找現有材質
            ElementId materialId = new FilteredElementCollector(doc)
           .OfClass(typeof(Material))
           .Cast<Material>()
           .Where(m => m.Name == materialName)
          .Select(m => m.Id)
          .FirstOrDefault();

            if (materialId != ElementId.InvalidElementId)
            {
                return materialId; // 材質已存在
            }

            // 2. 如果找不到，建立新材質
            try
            {
                ElementId newMatId = Material.Create(doc, materialName);
                return newMatId;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("錯誤", $"無法建立材質 '{materialName}': {ex.Message}");
                return ElementId.InvalidElementId;
            }
        }

        /// <summary>
        /// 應用材質到元件族群 (如 柱、梁) 的 "類型" (FamilySymbol)。
        /// </summary>
        private void ApplyMaterialToComponentTypes(Document doc, ElementId materialId, List<BuiltInCategory> categories, BuiltInParameter materialParameter)
        {
            // 建立一個多重類別過濾器
            ElementMulticategoryFilter filter = new ElementMulticategoryFilter(categories);

            // 蒐集所有 "類型" (FamilySymbol)，而不是 "實體" (FamilyInstance)
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            var symbols = collector.OfClass(typeof(FamilySymbol)).WherePasses(filter).Cast<FamilySymbol>();

            foreach (FamilySymbol symbol in symbols)
            {
                try
                {
                    Parameter matParam = symbol.get_Parameter(materialParameter);
                    if (matParam != null && !matParam.IsReadOnly)
                    {
                        matParam.Set(materialId);
                    }
                }
                catch (Exception)
                {
                    // 某些族群類型可能不允許修改，忽略它們
                }
            }
        }

        /// <summary>
        /// 應用材質到系統族群 (如 牆、樓板) "類型" 的主要結構圖層。
        /// **警告：這會修改專案中 "所有" 該類型的元素！**
        /// </summary>
        private void ApplyMaterialToSystemTypes(Document doc, ElementId materialId, BuiltInCategory category)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            var types = collector.OfClass(typeof(HostObjAttributes)).OfCategory(category).Cast<HostObjAttributes>();

            foreach (HostObjAttributes hostType in types)
            {
                try
                {
                    CompoundStructure cs = hostType.GetCompoundStructure();
                    if (cs == null) continue;

                    // 尋找第一個 "結構" 圖層並修改其材質
                    // 注意：一個複雜的牆/樓板可能有多個結構圖層，這裡僅修改第一個找到的
                    bool materialSet = false;
                    IList<CompoundStructureLayer> layers = cs.GetLayers();
                    for (int i = 0; i < cs.LayerCount; i++)
                    {
                        CompoundStructureLayer layer = layers[i];
                        if (layer.Function == MaterialFunctionAssignment.Structure)
                        {
                            cs.SetMaterialId(i, materialId);
                            materialSet = true;
                            break; // 找到並設定後就跳出
                        }
                    }

                    // 如果成功設定了材質，將修改後的結構寫回類型
                    if (materialSet)
                    {
                        hostType.SetCompoundStructure(cs);
                    }
                }
                catch (Exception)
                {
                    // 某些類型 (例如 帷幕牆) 可能沒有 CompoundStructure，忽略它們
                }
            }
        }

        /// <summary>
        /// 應用材質到所有 "鋼筋類型" (RebarBarType)。
        /// </summary>
        private void ApplyMaterialToRebarTypes(Document doc, ElementId materialId)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            var rebarTypes = collector.OfClass(typeof(RebarBarType)).Cast<RebarBarType>();

            foreach (RebarBarType barType in rebarTypes)
            {
                try
                {
                    // 鋼筋類型的材質參數是 MATERIAL_PARAM
                    Parameter matParam = barType.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                    if (matParam != null && !matParam.IsReadOnly)
                    {
                        matParam.Set(materialId);
                    }
                }
                catch (Exception)
                {
                    // 忽略可能發生的錯誤
                }
            }
        }
    }
}

