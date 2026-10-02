using Cloris.Aion2Flow.Resources.Catalog;
using Cloris.Aion2Flow.Services;

namespace Cloris.Aion2Flow.Tests.Resources;

public sealed class LocalizationServicesTests
{
    [Fact]
    public void LanguageAndResourcesStayEnglishWhenLegacySettingsRequestOtherLanguages()
    {
        var language = new LanguageService();
        using var localization = new LocalizationService(language);
        using var resources = new GameResourceService(language);
        Assert.Equal("en-US", language.CurrentLanguage);
        Assert.Single(LanguageService.SupportedLanguages);
        Assert.False(language.SetLanguage("zh-TW"));
        Assert.False(language.SetLanguage("ko-KR"));
        Assert.False(language.SetLanguage("de-DE"));
        Assert.Equal("Ready", localization["Status_Ready"]);
        Assert.Equal("en-US", resources.CurrentLanguage);
        Assert.Equal(ResourceCatalog.Load().Skills[2011101].Name, resources.ResolveSkillName(2011101));
    }
    [Theory]
    [InlineData(100014, "Fire Spirit: Basic Attack", "ICON_EL_SKILL_010.webp")]
    [InlineData(100018, "Fire Spirit: Basic Attack", "ICON_EL_SKILL_010.webp")]
    [InlineData(100024, "Water Spirit: Basic Attack", "ICON_EL_SKILL_011.webp")]
    [InlineData(100028, "Water Spirit: Basic Attack", "ICON_EL_SKILL_011.webp")]
    [InlineData(100034, "Wind Spirit: Basic Attack", "ICON_EL_SKILL_012.webp")]
    [InlineData(100048, "Earth Spirit: Basic Attack", "ICON_EL_SKILL_013.webp")]
    [InlineData(17040257, "Divine Punishment", "ICON_CL_SKILL_005.webp")]
    [InlineData(170402571, "Divine Punishment", "ICON_CL_SKILL_005.webp")]
    [InlineData(16030047, "Earth Tremor", "ICON_EL_SKILL_003.webp")]
    [InlineData(160300471, "Earth Tremor", "ICON_EL_SKILL_003.webp")]
    [InlineData(17440047, "Noble Aura", "ICON_CL_SKILL_046.webp")]
    [InlineData(17730001, "Empyrean Lord's Grace", "ICON_CL_SKILL_Passive_012.webp")]
    [InlineData(3001110, "Theostone: Charna's Root", "Icon_Item_Usable_Godstone_WP_r_004.webp")]
    [InlineData(30011101, "Theostone: Charna's Root", "Icon_Item_Usable_Godstone_WP_r_004.webp")]
    [InlineData(3000122, "Theostone: Charna's Acumen", "Icon_Item_Usable_Godstone_WP_r_016.webp")]
    public void GameResourceService_Resolves_Display_Resources_For_Packet_Variants(int skillCode, string expectedName, string expectedIcon)
    {
        try
        {
            var languageService = new LanguageService();
            languageService.SetLanguage(LanguageService.English);
            languageService.SetLanguage(LanguageService.English);
            using var resources = new GameResourceService(languageService);

            Assert.Equal(expectedName, resources.ResolveSkillName(skillCode));
            Assert.Equal(expectedIcon, resources.ResolveSkillIconAssetName(skillCode));
        }
        finally
        {
            CombatResourceRegistry.LoadSkillMap(LanguageService.English);
        }
    }

    [Theory]
    [InlineData(17270040, "Salvation", "ICON_CL_SKILL_026.webp")]
    [InlineData(17270047, "Salvation", "ICON_CL_SKILL_026.webp")]
    [InlineData(17280010, "Power Burst", "ICON_CL_SKILL_027.webp")]
    [InlineData(17290000, "Absolution", "ICON_CL_SKILL_028.webp")]
    [InlineData(17420010, "Yustiel's Power", "ICON_CL_SKILL_042.webp")]
    public void GameResourceService_Resolves_Cleric_Stigma_Display_Resources(int skillCode, string expectedName, string expectedIcon)
    {
        try
        {
            var languageService = new LanguageService();
            languageService.SetLanguage(LanguageService.English);
            languageService.SetLanguage(LanguageService.English);
            using var resources = new GameResourceService(languageService);

            Assert.Equal(expectedName, resources.ResolveSkillName(skillCode));
            Assert.Equal(expectedIcon, resources.ResolveSkillIconAssetName(skillCode));
        }
        finally
        {
            CombatResourceRegistry.LoadSkillMap(LanguageService.English);
        }
    }

    [Theory]
    [InlineData(1227237, "Attack", "ICON_TE_SKILL_001.webp")]
    [InlineData(1227265, "Wraith Surge", "ICON_TE_SKILL_001.webp")]
    public void GameResourceService_Resolves_Client_SkillDat_Display_Names(int skillCode, string expectedName, string expectedIcon)
    {
        try
        {
            var languageService = new LanguageService();
            languageService.SetLanguage(LanguageService.English);
            languageService.SetLanguage(LanguageService.English);
            using var resources = new GameResourceService(languageService);

            Assert.Equal(expectedName, resources.ResolveSkillName(skillCode));
            Assert.Equal(expectedIcon, resources.ResolveSkillIconAssetName(skillCode));
        }
        finally
        {
            CombatResourceRegistry.LoadSkillMap(LanguageService.English);
        }
    }

    [Fact]
    public void GameResourceService_Uses_SkillDat_Alias_Display_Id()
    {
        try
        {
            var languageService = new LanguageService();
            languageService.SetLanguage(LanguageService.English);
            languageService.SetLanguage(LanguageService.English);
            using var resources = new GameResourceService(languageService);

            Assert.Equal("Divine Punishment", resources.ResolveSkillName(17040257));
            Assert.Equal("ICON_CL_SKILL_005.webp", resources.ResolveSkillIconAssetName(17040257));
        }
        finally
        {
            CombatResourceRegistry.LoadSkillMap(LanguageService.English);
        }
    }

    [Fact]
    public void GameResourceService_Resolves_RowBase_For_SameFamily_Skill()
    {
        var languageService = new LanguageService();
        languageService.SetLanguage(LanguageService.English);
        using var resources = new GameResourceService(languageService);

        Assert.Equal(13_160_000, resources.ResolveBaseSkillIdForCode(13_160_007));
        Assert.Equal(13_160_000, resources.ResolveBaseSkillIdForCode(13_160_000));
    }

    [Theory]
    [InlineData(11_250_000, true)]
    [InlineData(11_250_010, true)]
    [InlineData(2_210_103, false)]
    [InlineData(3_001_110, false)]
    public void GameResourceService_Identifies_Player_Profession_Skills(int skillCode, bool expected)
    {
        var languageService = new LanguageService();
        languageService.SetLanguage(LanguageService.English);
        using var resources = new GameResourceService(languageService);

        Assert.Equal(expected, resources.IsPlayerProfessionSkill(skillCode));
    }

    [Theory]
    [InlineData(12130030, "Poach")]
    [InlineData(12780001, "Fury")]
    [InlineData(2210103, "Speed Scroll")]
    [InlineData(11190000, "Leaping Slam")]
    [InlineData(13130000, "Insignia Explosion")]
    [InlineData(13050000, "Flash Slice")]
    public void GameResourceService_Resolves_Runtime_Aura_And_Cooldown_RowBase_Ids(int resourceId, string expectedName)
    {
        try
        {
            var languageService = new LanguageService();
            languageService.SetLanguage(LanguageService.English);
            languageService.SetLanguage(LanguageService.English);
            using var resources = new GameResourceService(languageService);

            Assert.Equal(expectedName, resources.ResolveSkillName(resourceId));
        }
        finally
        {
            CombatResourceRegistry.LoadSkillMap(LanguageService.English);
        }
    }

    [Theory]
    [InlineData(1607415, "Attack", "ICON_TE_SKILL_001.webp")]
    [InlineData(1607400, "Attack", "ICON_TE_SKILL_001.webp")]
    public void GameResourceService_Resolves_Exact_Client_Skills_Without_Player_Family_Fallback(int skillCode, string expectedName, string expectedIcon)
    {
        try
        {
            var languageService = new LanguageService();
            languageService.SetLanguage(LanguageService.English);
            languageService.SetLanguage(LanguageService.English);
            using var resources = new GameResourceService(languageService);

            Assert.Equal(expectedName, resources.ResolveSkillName(skillCode));
            Assert.Equal(expectedIcon, resources.ResolveSkillIconAssetName(skillCode));
        }
        finally
        {
            CombatResourceRegistry.LoadSkillMap(LanguageService.English);
        }
    }
}
