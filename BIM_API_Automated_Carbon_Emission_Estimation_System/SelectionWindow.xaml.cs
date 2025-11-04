using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows; // 引用 WPF

namespace BIM_API_Automated_Carbon_Emission_Estimation_System
{
    /// <summary>
    /// SelectionWindow.xaml 的互動邏輯
    /// </summary>
    public partial class SelectionWindow : Window
    {
        private Document _doc;

        /// <param name="doc">當前的 Revit Document</param>
        public SelectionWindow(Document doc)
        {
            _doc = doc;
            InitializeComponent();
            PopulateLevelList();
        }

        /// <summary>
        /// 填充 UI 介面上的樓層列表
        /// </summary>
        private void PopulateLevelList()
        {
            try
            {
                var levels = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                LevelsListBox.ItemsSource = levels;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("錯誤", "無法載入樓層清單: " + ex.Message);
            }
        }

        /// <summary>
        /// "取消" 按鈕點擊事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// "開始擷取" 按鈕點擊事件
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. 獲取樓層篩選器
                List<ElementId> selectedLevelIds = LevelsListBox.SelectedItems
                    .Cast<Level>()
                    .Select(l => l.Id)
                    .ToList();

                // 2. 獲取類別篩選器
                Dictionary<BuiltInCategory, bool> categoryFilters = new Dictionary<BuiltInCategory, bool>
                {
                    { BuiltInCategory.OST_Walls, CheckWalls.IsChecked == true },
                    { BuiltInCategory.OST_Floors, CheckFloors.IsChecked == true },
                    { BuiltInCategory.OST_StructuralColumns, CheckColumns.IsChecked == true },
                    { BuiltInCategory.OST_StructuralFraming, CheckFraming.IsChecked == true },
                    { BuiltInCategory.OST_Rebar, CheckRebar.IsChecked == true }
                };

                // 3. 執行擷取
                List<ExtractedCarbonData> extractedData = new List<ExtractedCarbonData>();

                if (categoryFilters[BuiltInCategory.OST_Walls])
                    ExtractHostData(_doc, BuiltInCategory.OST_Walls, extractedData, "牆", selectedLevelIds);

                if (categoryFilters[BuiltInCategory.OST_Floors])
                    ExtractHostData(_doc, BuiltInCategory.OST_Floors, extractedData, "樓板", selectedLevelIds);

                if (categoryFilters[BuiltInCategory.OST_StructuralColumns])
                    ExtractComponentData(_doc, BuiltInCategory.OST_StructuralColumns, extractedData, "結構柱", selectedLevelIds);

                if (categoryFilters[BuiltInCategory.OST_StructuralFraming])
                    ExtractComponentData(_doc, BuiltInCategory.OST_StructuralFraming, extractedData, "結構梁", selectedLevelIds);

                if (categoryFilters[BuiltInCategory.OST_Rebar])
                    ExtractRebarData(_doc, extractedData, selectedLevelIds);

                // 4. 顯示報告
                ShowReport(extractedData);

                // 5. 關閉 UI 視窗
                this.Close();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("擷取錯誤", ex.Message);
            }
        }


        // -------------------------------------------------------------------
        // 以下是從 IExternalCommand 搬移過來的擷取邏輯
        // -------------------------------------------------------------------

        /// <summary>
        /// 建立一個樓層篩選器 (LogicalOrFilter)
        /// ** (已修正：使用正確的內建參數) **
        /// </summary>
        private ElementFilter CreateLevelFilter(List<ElementId> levelIds)
        {
            if (levelIds == null || levelIds.Count == 0)
                return null; // 如果沒有選擇樓層，則不過濾

            IList<ElementFilter> levelFilters = new List<ElementFilter>();

            foreach (ElementId levelId in levelIds)
            {
                // 篩選 "Level" 參數 (適用於 牆、樓板等)
                // **修正：使用 ELEM_LEVEL_PARAM**
                var providerWall = new ParameterValueProvider(new ElementId(BuiltInParameter.SCHEDULE_LEVEL_PARAM));
                var ruleWall = new FilterElementIdRule(providerWall, new FilterNumericEquals(), levelId);
                levelFilters.Add(new ElementParameterFilter(ruleWall));

                // 篩選 "Base Constraint" / "SCHEDULE_LEVEL_PARAM" (適用於 柱)
                var providerBase = new ParameterValueProvider(new ElementId(BuiltInParameter.SCHEDULE_LEVEL_PARAM));
                var ruleBase = new FilterElementIdRule(providerBase, new FilterNumericEquals(), levelId);
                levelFilters.Add(new ElementParameterFilter(ruleBase));

                // 篩選 "Reference Level" (適用於 梁)
                var providerRef = new ParameterValueProvider(new ElementId(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM));
                var ruleRef = new FilterElementIdRule(providerRef, new FilterNumericEquals(), levelId);
                levelFilters.Add(new ElementParameterFilter(ruleRef));
            }

            // 將所有規則用 "OR" 組合起來 (只要符合任一樓層即可)
            return new LogicalOrFilter(levelFilters);
        }

        /// <summary>
        /// 擷取系統族群 (Host Objects) 如 牆 和 樓板 的資料
        /// </summary>
        private void ExtractHostData(Document doc, BuiltInCategory category, List<ExtractedCarbonData> dataList, string categoryName, List<ElementId> levelIds)
        {
            var collector = new FilteredElementCollector(doc)
                .OfCategory(category)
                .WhereElementIsNotElementType();

            // 應用樓層篩選
            ElementFilter levelFilter = CreateLevelFilter(levelIds);
            if (levelFilter != null)
                collector.WherePasses(levelFilter);

            var elements = collector.ToElements();

            foreach (Element elem in elements)
            {
                if (!(elem is HostObject host)) continue;

                string typeName = host.Name;
                string materialName = "N/A";
                ElementId materialId = ElementId.InvalidElementId;

                try
                {
                    HostObjAttributes hostType = doc.GetElement(host.GetTypeId()) as HostObjAttributes;
                    CompoundStructure cs = hostType.GetCompoundStructure();
                    if (cs != null)
                    {
                        int structLayerIndex = cs.GetFirstCoreLayerIndex();
                        if (structLayerIndex > -1)
                        {
                            materialId = cs.GetMaterialId(structLayerIndex);
                        }
                    }
                }
                catch { /* 忽略 */ }

                if (materialId != ElementId.InvalidElementId)
                {
                    materialName = (doc.GetElement(materialId) as Material)?.Name ?? "N/A";
                }

                // **使用 Revit 2021+ (UnitTypeId) 語法**
                double volume_ft3 = elem.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsDouble();
                double volume_m3 = UnitUtils.Convert(volume_ft3, UnitTypeId.CubicFeet, UnitTypeId.CubicMeters);

                double area_ft2 = elem.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED).AsDouble();
                double area_m2 = UnitUtils.Convert(area_ft2, UnitTypeId.SquareFeet, UnitTypeId.SquareMeters);

                dataList.Add(new ExtractedCarbonData
                {
                    ElementId = elem.Id.ToString(),
                    Category = categoryName,
                    TypeName = typeName,
                    MaterialName = materialName,
                    Volume_m3 = Math.Round(volume_m3, 4),
                    Area_m2 = Math.Round(area_m2, 4),
                    Unit = "m³ & m²"
                });
            }
        }

        /// <summary>
        /// 擷取元件族群 (Component) 如 柱 和 梁 的資料
        /// </summary>
        private void ExtractComponentData(Document doc, BuiltInCategory category, List<ExtractedCarbonData> dataList, string categoryName, List<ElementId> levelIds)
        {
            var collector = new FilteredElementCollector(doc)
                .OfCategory(category)
                .WhereElementIsNotElementType();

            // 應用樓層篩選
            ElementFilter levelFilter = CreateLevelFilter(levelIds);
            if (levelFilter != null)
                collector.WherePasses(levelFilter);

            var elements = collector.ToElements();

            foreach (Element elem in elements)
            {
                if (!(elem is FamilyInstance inst)) continue;

                string typeName = inst.Symbol.Name;
                string materialName = "N/A";
                ElementId materialId = ElementId.InvalidElementId;

                try
                {
                    materialId = inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM)?.AsElementId();
                    if (materialId == null || materialId == ElementId.InvalidElementId)
                    {
                        // **修正：備用參數應為 MATERIAL_PARAM**
                        materialId = inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM)?.AsElementId();
                    }
                }
                catch { /* 忽略 */ }

                if (materialId != null && materialId != ElementId.InvalidElementId)
                {
                    materialName = (doc.GetElement(materialId) as Material)?.Name ?? "N/A";
                }

                // **使用 Revit 2021+ (UnitTypeId) 語法**
                double volume_ft3 = elem.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsDouble();
                double volume_m3 = UnitUtils.Convert(volume_ft3, UnitTypeId.CubicFeet, UnitTypeId.CubicMeters);

                dataList.Add(new ExtractedCarbonData
                {
                    ElementId = elem.Id.ToString(),
                    Category = categoryName,
                    TypeName = typeName,
                    MaterialName = materialName,
                    Volume_m3 = Math.Round(volume_m3, 4),
                    Unit = "m³"
                });
            }
        }

        /// <summary>
        /// 專門擷取 鋼筋 (Rebar) 的資料
        /// ** (已更新：使用 GetRebarWeight_kg 備援系統來計算重量) **
        /// </summary>
        private void ExtractRebarData(Document doc, List<ExtractedCarbonData> dataList, List<ElementId> levelIds)
        {
            // 1. 獲取所有鋼筋 (包含在群組中的)
            //    (GetAllRebarElements 是我們在下面新增的輔助方法)
            List<Rebar> allRebars = GetAllRebarElements(doc);

            // 2. 獲取樓層篩選器 (如果有的話)
            ElementFilter levelFilter = null;
            HashSet<ElementId> hostIdsOnLevel = null;

            if (levelIds != null && levelIds.Count > 0)
            {
                levelFilter = CreateLevelFilter(levelIds);

                List<BuiltInCategory> hostCategories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_Walls,
                    BuiltInCategory.OST_Floors,
                    BuiltInCategory.OST_StructuralColumns,
                    BuiltInCategory.OST_StructuralFraming,
                    BuiltInCategory.OST_StructuralFoundation
                };
                ElementMulticategoryFilter hostCatFilter = new ElementMulticategoryFilter(hostCategories);

                // 找到所有在這些樓層上的 "宿主" 元素
                hostIdsOnLevel = new FilteredElementCollector(doc)
                    .WherePasses(hostCatFilter)
                    .WherePasses(levelFilter)
                    .ToElementIds()
                    .ToHashSet(); // 使用 HashSet 以加速查詢
            }

            // 3. 遍歷所有鋼筋
            foreach (Rebar rebar in allRebars)
            {
                // 3a. 應用樓層篩選
                if (hostIdsOnLevel != null) // 檢查是否需要篩選樓層
                {
                    try
                    {
                        // 獲取鋼筋的宿主 ID
                        ElementId hostId = rebar.GetHostId();
                        if (hostId == null || !hostIdsOnLevel.Contains(hostId))
                        {
                            continue; // 如果宿主不在指定樓層上，則跳過此鋼筋
                        }
                    }
                    catch (Exception)
                    {
                        continue; // 獲取宿主失敗，跳過
                    }
                }

                // 3b. (同原邏輯) 擷取資料
                RebarBarType barType = doc.GetElement(rebar.GetTypeId()) as RebarBarType;
                if (barType == null) continue;

                string typeName = barType.Name;
                string materialName = "N/A";

                try
                {
                    ElementId materialId = barType.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM)?.AsElementId();
                    if (materialId != null && materialId != ElementId.InvalidElementId)
                    {
                        materialName = (doc.GetElement(materialId) as Material)?.Name ?? "N/A";
                    }
                }
                catch { /* 忽略 */ }

                // **更新：呼叫新的備援方法來計算重量**
                double totalWeight_kg = GetRebarWeight_kg(rebar, barType, doc);

                dataList.Add(new ExtractedCarbonData
                {
                    ElementId = rebar.Id.ToString(),
                    Category = "鋼筋",
                    TypeName = typeName,
                    MaterialName = materialName,
                    Weight_kg = Math.Round(totalWeight_kg, 2),
                    Unit = "kg"
                });
            }
        }

        /// <summary>
        /// (新方法) 取得鋼筋重量 (kg)，具備三階段備援系統。
        /// </summary>
        private double GetRebarWeight_kg(Rebar rebar, RebarBarType barType, Document doc)
        {
            double totalWeight_lbs = 0;

            // 獲取總長度 (內部單位: ft)
            double totalLength_ft = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_TOTAL_LENGTH).AsDouble();
            if (totalLength_ft == 0) return 0;

            // --- 備援 1: 嘗試字串名稱 (相容舊樣板) ---
            Parameter wpl_Param_String = barType.LookupParameter("Bar Weight/Length");
            if (wpl_Param_String != null && wpl_Param_String.HasValue && wpl_Param_String.AsDouble() > 0)
            {
                // 單位: (ft) * (lbs/ft) = lbs
                totalWeight_lbs = totalLength_ft * wpl_Param_String.AsDouble();
            }
            else
            {
                // --- 備援 2: 手動物理計算 (Volume * Density) (使用舊版 API) ---
                try
                {
                    // 1. 計算體積 (ft³)
                    double diameter_ft = barType.BarNominalDiameter;
                    double area_ft2 = Math.PI * Math.Pow(diameter_ft / 2.0, 2.0);
                    double volume_ft3 = totalLength_ft * area_ft2;

                    // 2. 獲取材料密度 (lb/ft³) - (使用舊版 API)
                    double density_lb_per_ft3 = 0;
                    ElementId materialId = barType.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM)?.AsElementId();
                    if (materialId != null && materialId != ElementId.InvalidElementId)
                    {
                        Material mat = doc.GetElement(materialId) as Material;
                        if (mat != null)
                        {
                            ElementId sAssetId = mat.StructuralAssetId;
                            if (sAssetId != ElementId.InvalidElementId)
                            {
                                // 舊版 API (RVT 2021 及更早) 的用法
                                PropertySetElement pset = doc.GetElement(sAssetId) as PropertySetElement;
                                if (pset != null)
                                {
                                    StructuralAsset sAsset = pset.GetStructuralAsset();
                                    density_lb_per_ft3 = sAsset.Density; // 直接獲取 double 屬性
                                }
                            }
                        }
                    }

                    // 3. 計算重量
                    if (density_lb_per_ft3 > 0)
                    {
                        // 單位: (ft³) * (lb/ft³) = lbs
                        totalWeight_lbs = volume_ft3 * density_lb_per_ft3;
                    }
                }
                catch (Exception ex)
                {
                    Debug.Print($"計算鋼筋重量失敗 (ID: {rebar.Id}): {ex.Message}");
                    totalWeight_lbs = 0; // 計算失敗
                }
            }

            // 最後才進行單位換算
            if (totalWeight_lbs > 0)
            {
                return UnitUtils.Convert(totalWeight_lbs, UnitTypeId.PoundsMass, UnitTypeId.Kilograms);
            }

            return 0; // 所有方法都失敗
        }


        /// <summary>
        /// (新方法) 獲取專案中所有的 Rebar 元素，包含在群組中的。
        /// </summary>
        private List<Rebar> GetAllRebarElements(Document doc)
        {
            List<Rebar> rebarList = new List<Rebar>();
            HashSet<ElementId> processedIds = new HashSet<ElementId>();

            // 1. 獲取所有獨立的 Rebar
            foreach (Rebar rebar in new FilteredElementCollector(doc).OfClass(typeof(Rebar)).WhereElementIsNotElementType().Cast<Rebar>())
            {
                if (processedIds.Add(rebar.Id))
                {
                    rebarList.Add(rebar);
                }
            }

            // 2. 遞迴獲取群組中的 Rebar
            var groups = new FilteredElementCollector(doc)
                .OfClass(typeof(Group))
                .WhereElementIsNotElementType()
                .Cast<Group>();

            foreach (Group group in groups)
            {
                GetRebarFromGroup(doc, group, rebarList, processedIds);
            }

            return rebarList;
        }

        /// <summary>
        /// (新方法) 遞迴輔助方法：從群組及其巢狀群組中獲取 Rebar。
        /// </summary>
        private void GetRebarFromGroup(Document doc, Group group, List<Rebar> rebarList, HashSet<ElementId> processedIds)
        {
            // 獲取群組的 "直接" 成員
            foreach (ElementId memberId in group.GetMemberIds())
            {
                Element member = doc.GetElement(memberId);

                if (member is Rebar rebar)
                {
                    if (processedIds.Add(rebar.Id)) // 僅在尚未處理過時才加入
                    {
                        rebarList.Add(rebar);
                    }
                }
                else if (member is Group nestedGroup) // 處理巢狀群組 (遞迴)
                {
                    GetRebarFromGroup(doc, nestedGroup, rebarList, processedIds);
                }
            }
        }


        /// <summary>
        /// 將擷取的資料格式化並顯示在 TaskDialog 中
        /// </summary>
        private void ShowReport(List<ExtractedCarbonData> dataList)
        {
            if (dataList.Count == 0)
            {
                TaskDialog.Show("資料擷取", "在指定的篩選條件下，沒有找到任何元素。", TaskDialogCommonButtons.Close);
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"已成功擷取 {dataList.Count} 筆元素資料。");
            sb.AppendLine("--- (僅顯示前 50 筆) ---");
            sb.AppendLine();
            sb.AppendLine("ID\t | 類別\t | 類型名稱\t | 材質名稱\t | 數值\t | 單位");
            sb.AppendLine("--------------------------------------------------------------------------------------------------");

            foreach (var data in dataList.Take(50))
            {
                string valueStr = "";
                if (data.Unit == "kg")
                    valueStr = data.Weight_kg.ToString();
                else if (data.Unit == "m³")
                    valueStr = data.Volume_m3.ToString();
                else if (data.Unit == "m³ & m²")
                    valueStr = $"{data.Volume_m3} / {data.Area_m2}";

                sb.AppendLine($"{data.ElementId}\t | {data.Category}\t | {data.TypeName}\t | {data.MaterialName}\t | {valueStr}\t | {data.Unit}");
            }

            TaskDialog mainDialog = new TaskDialog("BIM 資料擷取報告");
            mainDialog.MainInstruction = "資料擷取完成";
            mainDialog.MainContent = sb.ToString();
            mainDialog.Show();
        }
    }

    /// <summary>
    /// 用於儲存擷取資料的輔助類別 (Helper Class)
    /// </summary>
    public class ExtractedCarbonData
    {
        public string ElementId { get; set; }
        public string Category { get; set; }
        public string TypeName { get; set; }
        public string MaterialName { get; set; }
        public double Volume_m3 { get; set; } // 立方公尺
        public double Area_m2 { get; set; }   // 平方公尺
        public double Weight_kg { get; set; } // 公斤
        public string Unit { get; set; }      // 數據的主要計算單位
    }
}
