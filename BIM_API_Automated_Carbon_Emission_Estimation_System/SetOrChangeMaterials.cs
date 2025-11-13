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

            if (mainDialog.Show() == TaskDialogResult.Cancel)
            {
                message = "操作已由使用者取消。";
                return Result.Cancelled;
            }

            try
            {
                using (Transaction tx = new Transaction(doc, "應用碳排資料庫材質"))
                {
                    tx.Start();

                    // 1) 先確保兩個材質存在（修正 null 判斷邏輯）
                    // 1) 先確保兩個材質存在（存在時詢問是否覆蓋）
                    ElementId concreteMatId = GetOrCreateMaterial(doc, _concreteMaterialName, materialClass: "Concrete", onExists: MaterialExistBehavior.Ask);
                    ElementId rebarMatId = GetOrCreateMaterial(doc, _rebarMaterialName, materialClass: "Metal", onExists: MaterialExistBehavior.Ask);

                    if (concreteMatId == ElementId.InvalidElementId || rebarMatId == ElementId.InvalidElementId)
                    {
                        message = "無法找到或建立必要的材質。";
                        tx.RollBack();
                        return Result.Failed;
                    }

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

                    tx.Commit();
                }

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


