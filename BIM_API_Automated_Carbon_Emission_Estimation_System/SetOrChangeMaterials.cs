//using Autodesk.Revit.ApplicationServices;
//using Autodesk.Revit.Attributes;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.DB.Structure;
//using Autodesk.Revit.UI;
//using System;
//using System.Collections.Generic;
//using System.Linq;

<<<<<<< HEAD
namespace BIM_API_Automated_Carbon_Emission_Estimation_System
{
    [Transaction(TransactionMode.Manual)]
    public class SetOrChangeMaterials : IExternalCommand
    {
        private const string _concreteMaterialName = "預拌混凝土(280kgf/cm2, 飛灰爐石替代率30%)";
        private const string _rebarMaterialName = "鋼筋混凝土用鋼筋(SD280W)";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            TaskDialog mainDialog = new TaskDialog("警告：即將修改模型");
            mainDialog.MainInstruction = "您確定要繼續嗎？";
            mainDialog.MainContent = "此操作將批次修改專案中所有柱、梁、牆、樓板與鋼筋的「類型」材質。";
            mainDialog.CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel;
            mainDialog.DefaultButton = TaskDialogResult.Cancel;
=======
//namespace BIM_API_Automated_Carbon_Emission_Estimation_System
//{
//    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
//    public class SetOrChangeMaterials : IExternalCommand
//    {

//        private List<string> _concreteMaterialNames = new List<string>
//        {
//            "預拌混凝土(280kgf/cm2, 飛灰爐石替代率30%)",
//            "預拌混凝土(280kgf/cm2, 飛灰爐石替代率50%)",
//            "預拌混凝土(350kgf/cm2, 飛灰爐石替代率30%)",
//            "預拌混凝土(350kgf/cm2, 飛灰爐石替代率50%)",
//            "預拌混凝土(140 kgf/cm2)",
//            "預拌混凝土(175 kgf/cm2)",
//            "預拌混凝土(210 kgf/cm2)",
//            "預拌水中混凝土(210 kgf/cm2)",
//            "預拌混凝土(245 kgf/cm2)",
//            "預拌混凝土(280 kgf/cm2)",
//            "自充填預拌混凝土(350 kgf/cm2)"
//        };

//        // 鋼筋 (共 2 項) [cite: BIM 碳排資料.docx, Image 3]
//        private List<string> _rebarMaterialNames = new List<string>
//        {
//            "鋼筋混凝土用鋼筋(SD280W)",
//            "鋼筋混凝土用鋼筋(SD420W)"
//        };
>>>>>>> f8d7af572f91ed590c006e49c0fe234c79925401

//        public Result Execute(
//            ExternalCommandData commandData,
//            ref string message,
//            ElementSet elements)
//        {
//            UIApplication uiapp = commandData.Application;
//            UIDocument uidoc = uiapp.ActiveUIDocument;
//            Application app = uiapp.Application;
//            Document doc = uidoc.Document;

<<<<<<< HEAD
            try
            {
                using (Transaction tx = new Transaction(doc, "應用碳排資料庫材質"))
                {
                    tx.Start();

                    // 1) 先確保兩個材質存在（修正 null 判斷邏輯）
                    // 1) 先確保兩個材質存在（存在時詢問是否覆蓋）
                    ElementId concreteMatId = GetOrCreateMaterial(doc, _concreteMaterialName, materialClass: "Concrete", onExists: MaterialExistBehavior.Ask);
                    ElementId rebarMatId = GetOrCreateMaterial(doc, _rebarMaterialName, materialClass: "Metal", onExists: MaterialExistBehavior.Ask);
=======
//            // 執行前的最後警告
//            TaskDialog mainDialog = new TaskDialog("警告：即將修改模型");
//            mainDialog.MainInstruction = "您確定要繼續嗎？";
//            mainDialog.MainContent = "此操作將批次修改您專案中「所有」的柱、梁、牆、樓板和鋼筋的「類型」材質。\n\n";
//            mainDialog.CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel;
//            mainDialog.DefaultButton = TaskDialogResult.Cancel;

//            if (mainDialog.Show() == TaskDialogResult.Cancel)
//            {
//                message = "操作已由使用者取消。";
//                return Result.Cancelled;
//            }
>>>>>>> f8d7af572f91ed590c006e49c0fe234c79925401

//            try
//            {
//                // 啟動一個主事務 (Transaction) 來包覆所有變更
//                using (Transaction tx = new Transaction(doc, "應用碳排資料庫材質"))
//                {
//                    tx.Start();

<<<<<<< HEAD
                    // 2) 柱、梁 -> 混凝土
                    ApplyMaterialToComponentTypes(
                        doc,
                        concreteMatId,
                        new List<BuiltInCategory> {
                            BuiltInCategory.OST_StructuralColumns,
                            BuiltInCategory.OST_StructuralFraming
                        },
                        BuiltInParameter.STRUCTURAL_MATERIAL_PARAM
                    );

                    // 3) 牆 / 樓板 -> 混凝土（第一個結構層）
                    ApplyMaterialToSystemTypes(doc, concreteMatId, BuiltInCategory.OST_Walls);
                    ApplyMaterialToSystemTypes(doc, concreteMatId, BuiltInCategory.OST_Floors);

                    // 4) 鋼筋 -> SD280W
                    ApplyMaterialToRebarTypes(doc, rebarMatId);
=======
//                    // --- 步驟 1: 取得或建立材質 ---
//                    // 呼叫輔助方法來確保材質存在於專案中
//                    ElementId concreteMatId = GetOrCreateMaterial(doc, _concreteMaterialName);
//                    ElementId rebarMatId = GetOrCreateMaterial(doc, _rebarMaterialName);

//                    if (concreteMatId == ElementId.InvalidElementId || rebarMatId == ElementId.InvalidElementId)
//                    {
//                        message = "無法找到或建立必要的材質。";
//                        tx.RollBack();
//                        return Result.Failed;
//                    }

//                    // --- 步驟 2: 應用材質到元件族群 (柱、梁) ---
//                    List<BuiltInCategory> componentCategories = new List<BuiltInCategory>
//                    {
//                        BuiltInCategory.OST_StructuralColumns, // 結構柱
//                        BuiltInCategory.OST_StructuralFraming  // 結構框架 (梁)
//                    };
//                    ApplyMaterialToComponentTypes(doc, concreteMatId, componentCategories, BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
>>>>>>> f8d7af572f91ed590c006e49c0fe234c79925401

//                    // --- 步驟 3: 應用材質到系統族群 (牆、樓板) ---
//                    ApplyMaterialToSystemTypes(doc, concreteMatId, BuiltInCategory.OST_Walls);  // 牆
//                    ApplyMaterialToSystemTypes(doc, concreteMatId, BuiltInCategory.OST_Floors); // 樓板

<<<<<<< HEAD
                TaskDialog.Show("成功", $"已套用材質：\n混凝土：{_concreteMaterialName}\n鋼筋：{_rebarMaterialName}");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = "發生未預期的錯誤: " + ex.Message;
                return Result.Failed;
            }
        }

        private enum MaterialExistBehavior
        {
            Keep,       // 直接保留既有材質
            Overwrite,  // 直接覆蓋既有材質
            Ask         // 跳對話框詢問
        }
        /// <summary>
        /// 確保材質存在；若已存在可選擇是否覆蓋（覆蓋目前只示範更新 MaterialClass，可擴充外觀/物理資產）
        /// </summary>
        private ElementId GetOrCreateMaterial(
            Document doc,
            string materialName,
            string materialClass = null,
            MaterialExistBehavior onExists = MaterialExistBehavior.Keep)
        {
            Material existing = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => string.Equals(m.Name?.Trim(), materialName?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                bool doOverwrite = false;

                switch (onExists)
                {
                    case MaterialExistBehavior.Overwrite:
                        doOverwrite = true;
                        break;

                    case MaterialExistBehavior.Keep:
                        doOverwrite = false;
                        break;

                    case MaterialExistBehavior.Ask:
                    default:
                        {
                            TaskDialog td = new TaskDialog("材質已存在");
                            td.MainInstruction = $"專案中已存在材質：\n「{materialName}」";
                            td.MainContent =
                                "是否要覆蓋其屬性？\n\n" +
                                "※ 覆蓋目前會更新 MaterialClass（例如 Concrete / Metal）。\n" +
                                "※ 如需一併覆蓋外觀/物理資產，可再擴充此函式。";
                            td.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                            td.DefaultButton = TaskDialogResult.No;

                            doOverwrite = (td.Show() == TaskDialogResult.Yes);
                        }
                        break;
                }

                if (doOverwrite)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(materialClass) &&
                            !string.Equals(existing.MaterialClass, materialClass, StringComparison.OrdinalIgnoreCase))
                        {
                            existing.MaterialClass = materialClass;
                        }
                    }
                    catch
                    {

                    }
                }

                return existing.Id;
            }

            // 建立新材質
            try
            {
                ElementId newId = Material.Create(doc, materialName);
                Material mat = doc.GetElement(newId) as Material;
                if (mat == null) return ElementId.InvalidElementId;

                if (!string.IsNullOrWhiteSpace(materialClass))
                {
                    try { mat.MaterialClass = materialClass; } catch { /* ignore */ }
                }
                return newId;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("材質建立失敗", $"無法建立材質「{materialName}」。\n原因：{ex.Message}");
                return ElementId.InvalidElementId;
            }
        }

        /// <summary>
        /// 把材質套到 FamilySymbol（柱/梁類型）指定參數
        /// </summary>
        private void ApplyMaterialToComponentTypes(Document doc, ElementId materialId, List<BuiltInCategory> categories, BuiltInParameter materialParameter)
        {
            var filter = new ElementMulticategoryFilter(categories);
            var symbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .WherePasses(filter)
                .Cast<FamilySymbol>();

            foreach (var symbol in symbols)
            {
                try
                {
                    Parameter p = symbol.get_Parameter(materialParameter);
                    if (p != null && !p.IsReadOnly && materialId != null && materialId != ElementId.InvalidElementId)
                        p.Set(materialId);
                }
                catch { /* 某些族不允許改，忽略 */ }
            }
        }

        /// <summary>
        /// 把材質套到 HostObjAttributes（牆/樓板）第一個結構層
        /// </summary>
        private void ApplyMaterialToSystemTypes(Document doc, ElementId materialId, BuiltInCategory category)
        {
            var types = new FilteredElementCollector(doc)
                .OfClass(typeof(HostObjAttributes))
                .OfCategory(category)
                .Cast<HostObjAttributes>();

            foreach (var hostType in types)
            {
                try
                {
                    var cs = hostType.GetCompoundStructure();
                    if (cs == null) continue;

                    bool materialSet = false;
                    var layers = cs.GetLayers();
                    for (int i = 0; i < cs.LayerCount; i++)
                    {
                        var layer = layers[i];
                        if (layer.Function == MaterialFunctionAssignment.Structure)
                        {
                            cs.SetMaterialId(i, materialId);
                            materialSet = true;
                            break;
                        }
                    }

                    if (materialSet)
                        hostType.SetCompoundStructure(cs);
                }
                catch { /* 例如帷幕牆等沒有 CompoundStructure，略過 */ }
            }
        }

        /// <summary>
        /// 把材質套到所有鋼筋類型（RebarBarType）
        /// </summary>
        private void ApplyMaterialToRebarTypes(Document doc, ElementId materialId)
        {
            var rebarTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>();

            foreach (var barType in rebarTypes)
            {
                try
                {
                    var p = barType.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                    if (p != null && !p.IsReadOnly && materialId != null && materialId != ElementId.InvalidElementId)
                        p.Set(materialId);
                }
                catch { /* 忽略不可改的型別 */ }
            }
        }
    }
}
=======
//                    // --- 步驟 4: 應用材質到鋼筋 (Rebar) ---
//                    ApplyMaterialToRebarTypes(doc, rebarMatId);

//                    tx.Commit();
//                }

//                TaskDialog.Show("成功", $"已成功將材質應用到 柱、梁、牆、樓板和鋼筋 類型。\n\n混凝土: {_concreteMaterialName}\n鋼筋: {_rebarMaterialName}");
//                return Result.Succeeded;
//            }
//            catch (Exception ex)
//            {
//                message = "發生未預期的錯誤: " + ex.Message;
//                return Result.Failed;
//            }
//        }

//        /// <summary>
//        /// 輔助方法：檢查材質是否存在，若否，則建立它。
//        /// </summary>
//        /// <returns>材質的 ElementId</returns>
//        private ElementId GetOrCreateMaterial(Document doc, string materialName)
//        {
//            // 1. 嘗試尋找現有材質
//            ElementId materialId = new FilteredElementCollector(doc)
//           .OfClass(typeof(Material))
//           .Cast<Material>()
//           .Where(m => m.Name == materialName)
//          .Select(m => m.Id)
//          .FirstOrDefault();

//            if (materialId != ElementId.InvalidElementId)
//            {
//                return materialId; // 材質已存在
//            }

//            // 2. 如果找不到，建立新材質
//            try
//            {
//                ElementId newMatId = Material.Create(doc, materialName);
//                return newMatId;
//            }
//            catch (Exception ex)
//            {
//                TaskDialog.Show("錯誤", $"無法建立材質 '{materialName}': {ex.Message}");
//                return ElementId.InvalidElementId;
//            }
//        }

//        /// <summary>
//        /// 應用材質到元件族群 (如 柱、梁) 的 "類型" (FamilySymbol)。
//        /// </summary>
//        private void ApplyMaterialToComponentTypes(Document doc, ElementId materialId, List<BuiltInCategory> categories, BuiltInParameter materialParameter)
//        {
//            // 建立一個多重類別過濾器
//            ElementMulticategoryFilter filter = new ElementMulticategoryFilter(categories);

//            // 蒐集所有 "類型" (FamilySymbol)，而不是 "實體" (FamilyInstance)
//            FilteredElementCollector collector = new FilteredElementCollector(doc);
//            var symbols = collector.OfClass(typeof(FamilySymbol)).WherePasses(filter).Cast<FamilySymbol>();

//            foreach (FamilySymbol symbol in symbols)
//            {
//                try
//                {
//                    Parameter matParam = symbol.get_Parameter(materialParameter);
//                    if (matParam != null && !matParam.IsReadOnly)
//                    {
//                        matParam.Set(materialId);
//                    }
//                }
//                catch (Exception)
//                {
//                    // 某些族群類型可能不允許修改，忽略它們
//                }
//            }
//        }

//        /// <summary>
//        /// 應用材質到系統族群 (如 牆、樓板) "類型" 的主要結構圖層。
//        /// **警告：這會修改專案中 "所有" 該類型的元素！**
//        /// </summary>
//        private void ApplyMaterialToSystemTypes(Document doc, ElementId materialId, BuiltInCategory category)
//        {
//            FilteredElementCollector collector = new FilteredElementCollector(doc);
//            var types = collector.OfClass(typeof(HostObjAttributes)).OfCategory(category).Cast<HostObjAttributes>();

//            foreach (HostObjAttributes hostType in types)
//            {
//                try
//                {
//                    CompoundStructure cs = hostType.GetCompoundStructure();
//                    if (cs == null) continue;

//                    // 尋找第一個 "結構" 圖層並修改其材質
//                    // 注意：一個複雜的牆/樓板可能有多個結構圖層，這裡僅修改第一個找到的
//                    bool materialSet = false;
//                    IList<CompoundStructureLayer> layers = cs.GetLayers();
//                    for (int i = 0; i < cs.LayerCount; i++)
//                    {
//                        CompoundStructureLayer layer = layers[i];
//                        if (layer.Function == MaterialFunctionAssignment.Structure)
//                        {
//                            cs.SetMaterialId(i, materialId);
//                            materialSet = true;
//                            break; // 找到並設定後就跳出
//                        }
//                    }

//                    // 如果成功設定了材質，將修改後的結構寫回類型
//                    if (materialSet)
//                    {
//                        hostType.SetCompoundStructure(cs);
//                    }
//                }
//                catch (Exception)
//                {
//                    // 某些類型 (例如 帷幕牆) 可能沒有 CompoundStructure，忽略它們
//                }
//            }
//        }

//        /// <summary>
//        /// 應用材質到所有 "鋼筋類型" (RebarBarType)。
//        /// </summary>
//        private void ApplyMaterialToRebarTypes(Document doc, ElementId materialId)
//        {
//            FilteredElementCollector collector = new FilteredElementCollector(doc);
//            var rebarTypes = collector.OfClass(typeof(RebarBarType)).Cast<RebarBarType>();

//            foreach (RebarBarType barType in rebarTypes)
//            {
//                try
//                {
//                    // 鋼筋類型的材質參數是 MATERIAL_PARAM
//                    Parameter matParam = barType.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
//                    if (matParam != null && !matParam.IsReadOnly)
//                    {
//                        matParam.Set(materialId);
//                    }
//                }
//                catch (Exception)
//                {
//                    // 忽略可能發生的錯誤
//                }
//            }
//        }
//    }
//}
>>>>>>> f8d7af572f91ed590c006e49c0fe234c79925401


