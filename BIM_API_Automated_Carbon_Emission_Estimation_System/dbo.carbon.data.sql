INSERT INTO [dbo].[carbon] ([Id], [name], [unit], [kgCO2e]) VALUES (NULL, NULL, NULL, NULL)
-- 預拌混凝土 (image_8f9f4c.png)
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (1, N'預拌混凝土(280kgf/cm2, 飛灰爐石替代率30%)', N'立方公尺(m3)', 301.00);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (2, N'預拌混凝土(280kgf/cm2, 飛灰爐石替代率50%)', N'立方公尺(m3)', 214.00);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (3, N'預拌混凝土(350kgf/cm2, 飛灰爐石替代率30%)', N'立方公尺(m3)', 341.00);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (4, N'預拌混凝土(350kgf/cm2, 飛灰爐石替代率50%)', N'立方公尺(m3)', 244.00);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (5, N'預拌混凝土(140 kgf/cm2)', N'立方公尺(m3)', 200.00);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (6, N'預拌混凝土(175 kgf/cm2)', N'立方公尺(m3)', 224.00);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (7, N'預拌混凝土(210 kgf/cm2)', N'立方公尺(m3)', 238.00);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (8, N'預拌水中混凝土(210 kgf/cm2)', N'立方公尺(m3)', 346.00);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (9, N'預拌混凝土(245 kgf/cm2)', N'立方公尺(m3)', 252.00);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (10, N'預拌混凝土(280 kgf/cm2)', N'立方公尺(m3)', 338.00);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (11, N'自充填預拌混凝土(350 kgf/cm2)', N'立方公尺(m3)', 375.00);

-- 水泥及熟料 (image_8f9f48.png)
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (12, N'卜特蘭水泥(II型)', N'公斤(kg)', 0.981);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (13, N'卜特蘭水泥(II型)', N'公斤(kg)', 0.964);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (14, N'鋁質水泥', N'公斤(kg)', 10.10);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (15, N'水泥熟料', N'公斤(kg)', 0.948);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (16, N'水泥(不分型號)', N'公斤(kg)', 0.907);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (17, N'水泥熟料', N'公斤(kg)', 0.950);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (18, N'卜特蘭水泥(乾式)', N'公斤(kg)', 0.940);

-- 鋼筋 (image_8f9f65.png)
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (19, N'鋼筋混凝土用鋼筋(SD280W)', N'公斤(kg)', 0.835);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (20, N'鋼筋混凝土用鋼筋(SD420W)', N'公斤(kg)', 0.834);

-- 裝修建材 (image_8f9f83.png, image_8f9f87.png) - 採「生產含運輸」的 CO2 排放量
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (21, N'牆面石材', N'平方公尺(m²)', 15.11);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (22, N'牆面磁磚', N'平方公尺(m²)', 19.10);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (23, N'地坪類磁磚', N'平方公尺(m²)', 25.16);
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (24, N'地坪類實木地板', N'平方公尺(m²)', 9.08);

-- 裝修玻璃 (image_8f9f6a.png)
INSERT INTO carbon ([Id], [name], [unit], [kgCO2e]) VALUES (25, N'裝修玻璃 - 合成', N'公斤(kg)', 0.75318);