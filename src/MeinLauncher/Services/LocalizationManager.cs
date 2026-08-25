using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace MeinLauncher.Services;

/// <summary>
/// Zentrale Lokalisierung für alle sichtbaren UI-Texte (Deutsch/Englisch).
/// Bietet benannte, bindbare Eigenschaften für XAML sowie <see cref="Get"/> für
/// formatierbare Meldungen aus den ViewModels.
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Wird nach jedem Sprachwechsel ausgelöst.</summary>
    public event Action? LanguageChanged;

    private string _language = "de";

    public string Language => _language;

    public void SetLanguage(string language)
    {
        var newLang = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "de";
        if (newLang == _language)
            return;

        _language = newLang;
        RaiseAll();
        LanguageChanged?.Invoke();
    }

    /// <summary>Übersetzter Text für einen Schlüssel, optional mit Format-Argumenten.</summary>
    public string Get(string key, params object[] args)
    {
        if (Strings.TryGetValue(key, out var pair))
        {
            var text = _language == "en" ? pair.En : pair.De;
            return args.Length == 0 ? text : string.Format(text, args);
        }

        return "?" + key;
    }

    private void RaiseAll()
    {
        foreach (var property in AllProperties)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }

    private readonly record struct Translation(string De, string En);

    // ---------------------------------------------------------------- Navigation
    public string NavStart => Get("Nav.Start");
    public string NavProfile => Get("Nav.Profile");
    public string NavSettings => Get("Nav.Settings");
    public string NavNews => Get("Nav.News");

    // ---------------------------------------------------------------- Branding
    public string BrandingSubtitle => Get("Branding.Subtitle");

    // ---------------------------------------------------------------- Update
    public string UpdateAvailable => Get("Update.Available");
    public string UpdateInstall => Get("Update.Install");
    public string UpdateLater => Get("Update.Later");
    public string UpdateFailed => Get("Update.Failed");

    // ---------------------------------------------------------------- Home
    public string HomeWelcomeBack => Get("Home.WelcomeBack");
    public string HomeNoVersionSelected => Get("Home.NoVersionSelected");
    public string HomePlay => Get("Home.Play");
    public string HomeActiveProfile => Get("Home.ActiveProfile");
    public string HomeManageProfiles => Get("Home.ManageProfiles");
    public string HomeReady => Get("Home.Ready");
    public string HomeDirectLaunchHint => Get("Home.DirectLaunchHint");
    public string HomeStartingGame => Get("Home.StartingGame");
    public string HomeGameStarted => Get("Home.GameStarted");
    public string HomeNotSignedIn => Get("Home.NotSignedIn");
    public string HomeVersionNotInstalled => Get("Home.VersionNotInstalled");
    public string HomeVersionDataError => Get("Home.VersionDataError");
    public string HomeJavaNotFound => Get("Home.JavaNotFound");
    public string HomeMissingLibraries => Get("Home.MissingLibraries");
    public string HomeGameLaunchFailed => Get("Home.GameLaunchFailed");
    public string HomeInstalledVersions => Get("Home.InstalledVersions");
    public string HomeModsInFolder => Get("Home.ModsInFolder");
    public string HomeActivePlayer => Get("Home.ActivePlayer");
    public string HomeAboutTitle => Get("Home.AboutTitle");
    public string HomeAboutText => Get("Home.AboutText");
    public string HomeProfile => Get("Home.Profile");
    public string HomeProfileStandard => Get("Home.ProfileStandard");
    public string HomeProfileNew => Get("Home.ProfileNew");
    public string HomeProfileRename => Get("Home.ProfileRename");
    public string HomeProfileDuplicate => Get("Home.ProfileDuplicate");
    public string HomeProfileDelete => Get("Home.ProfileDelete");
    public string HomeProfileDeleteConfirm => Get("Home.ProfileDeleteConfirm");
    public string HomeProfileNamePlaceholder => Get("Home.ProfileNamePlaceholder");
    public string HomeProfileOk => Get("Home.ProfileOk");
    public string HomeProfileCancel => Get("Home.ProfileCancel");
    public string HomeProfileHint => Get("Home.ProfileHint");

    // ---------------------------------------------------------------- Profile
    public string ProfileTitle => Get("Profile.Title");
    public string ProfileSubtitle => Get("Profile.Subtitle");
    public string ProfileBack => Get("Profile.Back");
    public string ProfileProfiles => Get("Profile.Profiles");
    public string ProfileNewProfile => Get("Profile.NewProfile");
    public string ProfileActiveProfile => Get("Profile.ActiveProfile");
    public string ProfileVersionLabel => Get("Profile.VersionLabel");
    public string ProfileManageVersions => Get("Profile.ManageVersions");
    public string ProfileLoaderLabel => Get("Profile.LoaderLabel");
    public string ProfileModCountLabel => Get("Profile.ModCountLabel");
    public string ProfileModCount(int count) => Get("Profile.ModCount", count);
    public string ProfileManageMods => Get("Profile.ManageMods");
    public string ProfileJavaRam => Get("Profile.JavaRam");
    public string ProfilePlay => Get("Profile.Play");
    public string ProfileVersions => Get("Profile.Versions");
    public string ProfileMods => Get("Profile.Mods");
    public string ProfileVersionsSubtitle => Get("Profile.VersionsSubtitle");
    public string ProfileModsSubtitle => Get("Profile.ModsSubtitle");

    // ---------------------------------------------------------------- News
    public string NewsTitle => Get("News.Title");
    public string NewsSubtitle => Get("News.Subtitle");
    public string NewsLoading => Get("News.Loading");
    public string NewsLoadError => Get("News.LoadError");
    public string NewsRetry => Get("News.Retry");
    public string NewsEmpty => Get("News.Empty");
    public string NewsWatch => Get("News.Watch");
    public string NewsMore => Get("News.More");
    public string NewsClose => Get("News.Close");
    public string NewsPlayerError => Get("News.PlayerError");

    // ---------------------------------------------------------------- Versions
    public string VersionsTitle => Get("Versions.Title");
    public string VersionsLoading => Get("Versions.Loading");
    public string VersionsLoaded => Get("Versions.Loaded");
    public string VersionsNoneFound => Get("Versions.NoneFound");
    public string VersionsLoadError => Get("Versions.LoadError");
    public string VersionsSearchPlaceholder => Get("Versions.SearchPlaceholder");
    public string VersionsRefresh => Get("Versions.Refresh");
    public string VersionsInstall => Get("Versions.Install");
    public string VersionsRelease => Get("Versions.Release");
    public string VersionsSnapshot => Get("Versions.Snapshot");
    public string VersionsInstalled => Get("Versions.Installed");
    public string VersionsDefault => Get("Versions.Default");
    public string VersionsSelectFirst => Get("Versions.SelectFirst");
    public string VersionsInstallSuccess => Get("Versions.InstallSuccess");
    public string VersionsInstallFailed => Get("Versions.InstallFailed");
    public string VersionsFilterAll => Get("Versions.FilterAll");

    // ---------------------------------------------------------------- Mods
    public string ModsTitle => Get("Mods.Title");
    public string ModsOpenFolder => Get("Mods.OpenFolder");
    public string ModsRefresh => Get("Mods.Refresh");
    public string ModsNoneFound => Get("Mods.NoneFound");
    public string ModsEmptyText => Get("Mods.EmptyText");
    public string ModsEmptyShort => Get("Mods.EmptyShort");
    public string ModsCount => Get("Mods.Count");
    public string ModsActive => Get("Mods.Active");
    public string ModsDisabled => Get("Mods.Disabled");
    public string ModsEnabled => Get("Mods.Enabled");
    public string ModsDisabledMsg => Get("Mods.DisabledMsg");
    public string ModsError => Get("Mods.Error");
    public string ModsSectionInstalled => Get("Mods.SectionInstalled");
    public string ModsSectionModrinth => Get("Mods.SectionModrinth");
    public string ModsSectionCurseForge => Get("Mods.SectionCurseForge");
    public string ModsCurseForgeIntro => Get("Mods.CurseForgeIntro");
    public string ModsCurseForgeNoApiKey => Get("Mods.CurseForgeNoApiKey");
    public string ModsCurseForgeDisabled => Get("Mods.CurseForgeDisabled");
    public string ModsCurseForgeDisabledHint => Get("Mods.CurseForgeDisabledHint");
    public string ModsGameVersionLabel => Get("Mods.GameVersionLabel");
    public string ModsLoaderLabel => Get("Mods.LoaderLabel");
    public string ModsLoaderFabric => Get("Mods.LoaderFabric");
    public string ModsLoaderForge => Get("Mods.LoaderForge");
    public string ModsLoaderNeoForge => Get("Mods.LoaderNeoForge");
    public string ModsLoaderQuilt => Get("Mods.LoaderQuilt");
    public string ModsLoaderLiteLoader => Get("Mods.LoaderLiteLoader");
    public string ModsSearchPlaceholder => Get("Mods.SearchPlaceholder");
    public string ModsSearchButton => Get("Mods.SearchButton");
    public string ModsInstall => Get("Mods.Install");
    public string ModsInstalling => Get("Mods.Installing");
    public string ModsInstalledBadge => Get("Mods.InstalledBadge");
    public string ModsUpdate => Get("Mods.Update");
    public string ModsUpdating => Get("Mods.Updating");
    public string ModsUninstall => Get("Mods.Uninstall");
    public string ModsNoVersionSelected => Get("Mods.NoVersionSelected");
    public string ModsNoResults => Get("Mods.NoResults");
    public string ModsSearchStartHint => Get("Mods.SearchStartHint");
    public string ModsSearchError => Get("Mods.SearchError");
    public string ModsBrowseButton => Get("Mods.BrowseButton");
    public string ModsModrinthIntro => Get("Mods.ModrinthIntro");
    public string ModsInstallSuccess => Get("Mods.InstallSuccess");
    public string ModsInstallNoVersion => Get("Mods.InstallNoVersion");
    public string ModsInstallAlready => Get("Mods.InstallAlready");
    public string ModsInstallError => Get("Mods.InstallError");
    public string ModsUpdateSuccess => Get("Mods.UpdateSuccess");
    public string ModsUpdateError => Get("Mods.UpdateError");
    public string ModsUninstalled => Get("Mods.Uninstalled");
    public string ModsUnknownMod => Get("Mods.UnknownMod");
    public string ModsUnknownVersion => Get("Mods.UnknownVersion");
    public string ModsUnknownLoader => Get("Mods.UnknownLoader");
    public string ModsDownloads(long downloads) => Get("Mods.Downloads", downloads.ToString("N0"));
    public string ModsFollows(long follows) => Get("Mods.Follows", follows.ToString("N0"));
    public string ModsUpdateAvailable(string version) => Get("Mods.UpdateAvailable", version);
    public string ModsInstalledWithDeps(int fileCount) => Get("Mods.InstalledWithDeps", fileCount);
    public string ModsInstallWithMissingDeps(int count, string names) => Get("Mods.InstallWithMissingDeps", count, names);
    public string ModsUpdateWithMissingDeps(int count, string names) => Get("Mods.UpdateWithMissingDeps", count, names);
    public string ModsGoToVersions => Get("Mods.GoToVersions");
    public string ModsIsLoading => Get("Mods.IsLoading");

    // ---------------------------------------------------------------- Settings
    public string SettingsTitle => Get("Settings.Title");
    public string SettingsSubtitle => Get("Settings.Subtitle");
    public string SettingsProfile => Get("Settings.Profile");
    public string SettingsUsername => Get("Settings.Username");
    public string SettingsUsernamePlaceholder => Get("Settings.UsernamePlaceholder");
    public string SettingsMicrosoftAccount => Get("Settings.MicrosoftAccount");
    public string SettingsMicrosoftAccountHint => Get("Settings.MicrosoftAccountHint");
    public string SettingsMicrosoftLoginButton => Get("Settings.MicrosoftLoginButton");
    public string SettingsMicrosoftLoginNoClientId => Get("Settings.MicrosoftLoginNoClientId");
    public string SettingsMicrosoftLoginOpeningBrowser => Get("Settings.MicrosoftLoginOpeningBrowser");
    public string SettingsMicrosoftLoginWaiting => Get("Settings.MicrosoftLoginWaiting");
    public string SettingsMicrosoftLoginChecking => Get("Settings.MicrosoftLoginChecking");
    public string SettingsMicrosoftLoggedInAs => Get("Settings.MicrosoftLoggedInAs");
    public string SettingsMicrosoftLoginCancelled => Get("Settings.MicrosoftLoginCancelled");
    public string SettingsMicrosoftLoginError => Get("Settings.MicrosoftLoginError");
    public string SettingsMicrosoftLoginRestoring => Get("Settings.MicrosoftLoginRestoring");
    public string SettingsJavaRuntime => Get("Settings.JavaRuntime");
    public string SettingsJavaPathHint => Get("Settings.JavaPathHint");
    public string SettingsBrowse => Get("Settings.Browse");
    public string SettingsJavaFound => Get("Settings.JavaFound");
    public string SettingsJavaNotFound => Get("Settings.JavaNotFound");
    public string SettingsDetectedRuntimes => Get("Settings.DetectedRuntimes");
    public string SettingsRescan => Get("Settings.Rescan");
    public string SettingsUse => Get("Settings.Use");
    public string SettingsGameData => Get("Settings.GameData");
    public string SettingsGameDataHint => Get("Settings.GameDataHint");
    public string SettingsGameDataSubfolders => Get("Settings.GameDataSubfolders");
    public string SettingsOpenExplorer => Get("Settings.OpenExplorer");
    public string SettingsResetDefault => Get("Settings.ResetDefault");
    public string SettingsRam => Get("Settings.Ram");
    public string SettingsRamHint => Get("Settings.RamHint");
    public string SettingsSound => Get("Settings.Sound");
    public string SettingsSoundHint => Get("Settings.SoundHint");
    public string SettingsSoundVolume => Get("Settings.SoundVolume");
    public string SettingsDesign => Get("Settings.Design");
    public string SettingsTheme => Get("Settings.Theme");
    public string SettingsDark => Get("Settings.Dark");
    public string SettingsLight => Get("Settings.Light");
    public string SettingsAccent => Get("Settings.Accent");
    public string SettingsTransparency => Get("Settings.Transparency");
    public string SettingsTransparencyNone => Get("Settings.TransparencyNone");
    public string SettingsTransparencyLight => Get("Settings.TransparencyLight");
    public string SettingsTransparencyStrong => Get("Settings.TransparencyStrong");
    public string SettingsBackground => Get("Settings.Background");
    public string SettingsBackgroundChoose => Get("Settings.BackgroundChoose");
    public string SettingsBackgroundReset => Get("Settings.BackgroundReset");
    public string SettingsLanguage => Get("Settings.Language");
    public string SettingsGerman => Get("Settings.German");
    public string SettingsEnglish => Get("Settings.English");
    public string SettingsSave => Get("Settings.Save");
    public string SettingsSaved => Get("Settings.Saved");
    public string SettingsSkinCapes => Get("Settings.SkinCapes");
    public string SettingsSkinCapesHint => Get("Settings.SkinCapesHint");
    public string SettingsSkinCapesRefresh => Get("Settings.SkinCapesRefresh");
    public string SettingsSkinCapesCount => Get("Settings.SkinCapesCount");
    public string SettingsBack => Get("Settings.Back");
    public string SettingsCategoryDesign => Get("Settings.CategoryDesign");
    public string SettingsCategoryDesignSubtitle => Get("Settings.CategoryDesignSubtitle");
    public string SettingsCategoryUi => Get("Settings.CategoryUi");
    public string SettingsCategoryUiSubtitle => Get("Settings.CategoryUiSubtitle");
    public string SettingsCategoryMinecraft => Get("Settings.CategoryMinecraft");
    public string SettingsCategoryMinecraftSubtitle => Get("Settings.CategoryMinecraftSubtitle");
    public string SettingsCategoryMods => Get("Settings.CategoryMods");
    public string SettingsCategoryModsSubtitle => Get("Settings.CategoryModsSubtitle");
    public string SettingsCategoryAccount => Get("Settings.CategoryAccount");
    public string SettingsCategoryAccountSubtitle => Get("Settings.CategoryAccountSubtitle");
    public string SettingsCategoryGeneral => Get("Settings.CategoryGeneral");
    public string SettingsCategoryGeneralSubtitle => Get("Settings.CategoryGeneralSubtitle");
    public string SettingsModsLoader => Get("Settings.ModsLoader");
    public string SettingsModsLoaderHint => Get("Settings.ModsLoaderHint");
    public string SettingsModsFolder => Get("Settings.ModsFolder");
    public string SettingsModsFolderHint => Get("Settings.ModsFolderHint");
    public string SettingsCurseForgeApiKey => Get("Settings.CurseForgeApiKey");
    public string SettingsCurseForgeApiKeyHint => Get("Settings.CurseForgeApiKeyHint");
    public string SettingsCurseForgeApiKeyInfo => Get("Settings.CurseForgeApiKeyInfo");
    public string SettingsUsernameHint => Get("Settings.UsernameHint");
    public string SettingsUninstallTitle => Get("Settings.UninstallTitle");
    public string SettingsUninstallConfirm => Get("Settings.UninstallConfirm");
    public string SettingsUninstallYes => Get("Settings.UninstallYes");
    public string SettingsUninstallNo => Get("Settings.UninstallNo");
    public string SettingsUninstallButton => Get("Settings.UninstallButton");
    public string SettingsUninstallHint => Get("Settings.UninstallHint");

    // ---------------------------------------------------------------- Hintergrund-Editor
    public string BackgroundEnabled => Get("Background.Enabled");
    public string BackgroundEnabledHint => Get("Background.EnabledHint");
    public string BackgroundImage => Get("Background.Image");
    public string BackgroundImageHint => Get("Background.ImageHint");
    public string BackgroundPreview => Get("Background.Preview");
    public string BackgroundPreviewOff => Get("Background.PreviewOff");
    public string BackgroundElements => Get("Background.Elements");
    public string BackgroundAnimation => Get("Background.Animation");
    public string BackgroundKind => Get("Background.Kind");
    public string BackgroundColor => Get("Background.Color");
    public string BackgroundCount => Get("Background.Count");
    public string BackgroundSize => Get("Background.Size");
    public string BackgroundDensity => Get("Background.Density");
    public string BackgroundOpacity => Get("Background.Opacity");
    public string BackgroundSpacing => Get("Background.Spacing");
    public string BackgroundAnimate => Get("Background.Animate");
    public string BackgroundSpeed => Get("Background.Speed");
    public string BackgroundRotate => Get("Background.Rotate");
    public string BackgroundRotationSpeed => Get("Background.RotationSpeed");
    public string BackgroundIntensity => Get("Background.Intensity");
    public string BackgroundReset => Get("Background.Reset");
    public string BackgroundKindPawn => Get("Background.Kind.Pawn");
    public string BackgroundKindKnight => Get("Background.Kind.Knight");
    public string BackgroundKindBishop => Get("Background.Kind.Bishop");
    public string BackgroundKindRook => Get("Background.Kind.Rook");
    public string BackgroundKindQueen => Get("Background.Kind.Queen");
    public string BackgroundKindKing => Get("Background.Kind.King");
    public string BackgroundKindCircle => Get("Background.Kind.Circle");
    public string BackgroundKindSquare => Get("Background.Kind.Square");
    public string BackgroundKindTriangle => Get("Background.Kind.Triangle");
    public string BackgroundKindDiamond => Get("Background.Kind.Diamond");
    public string BackgroundKindHexagon => Get("Background.Kind.Hexagon");
    public string BackgroundKindRing => Get("Background.Kind.Ring");
    public string BackgroundKindStar => Get("Background.Kind.Star");
    public string BackgroundKindParticle => Get("Background.Kind.Particle");

    // ---------------------------------------------------------------- Akzentfarben
    public string AccentGreen => Get("Accent.Green");    public string AccentRed => Get("Accent.Red");
    public string AccentBlue => Get("Accent.Blue");
    public string AccentOrange => Get("Accent.Orange");
    public string AccentPurple => Get("Accent.Purple");
    public string AccentCyan => Get("Accent.Cyan");
    public string AccentYellow => Get("Accent.Yellow");
    public string AccentWhite => Get("Accent.White");
    public string AccentBlack => Get("Accent.Black");
    public string AccentPink => Get("Accent.Pink");
    public string AccentSilver => Get("Accent.Silver");

    // ---------------------------------------------------------------- Diverses
    public string JavaUnknownVersion => Get("Java.UnknownVersion");

    private static readonly string[] AllProperties =
    [
        nameof(NavStart), nameof(NavProfile), nameof(NavSettings), nameof(NavNews),
        nameof(BrandingSubtitle),
        nameof(UpdateAvailable), nameof(UpdateInstall), nameof(UpdateLater), nameof(UpdateFailed),
        nameof(HomeWelcomeBack), nameof(HomeNoVersionSelected), nameof(HomePlay),
        nameof(HomeActiveProfile), nameof(HomeManageProfiles), nameof(HomeReady), nameof(HomeDirectLaunchHint),
        nameof(HomeStartingGame), nameof(HomeGameStarted), nameof(HomeNotSignedIn),
        nameof(HomeVersionNotInstalled), nameof(HomeVersionDataError), nameof(HomeJavaNotFound),
        nameof(HomeMissingLibraries), nameof(HomeGameLaunchFailed),
        nameof(HomeInstalledVersions), nameof(HomeModsInFolder), nameof(HomeActivePlayer),
        nameof(HomeAboutTitle), nameof(HomeAboutText),
        nameof(HomeProfile), nameof(HomeProfileStandard),
        nameof(HomeProfileNew), nameof(HomeProfileRename), nameof(HomeProfileDuplicate),
        nameof(HomeProfileDelete), nameof(HomeProfileDeleteConfirm),
        nameof(HomeProfileNamePlaceholder), nameof(HomeProfileOk), nameof(HomeProfileCancel),
        nameof(HomeProfileHint),
        nameof(ProfileTitle), nameof(ProfileSubtitle), nameof(ProfileBack),
        nameof(ProfileProfiles), nameof(ProfileNewProfile), nameof(ProfileActiveProfile),
        nameof(ProfileVersionLabel), nameof(ProfileManageVersions), nameof(ProfileLoaderLabel),
        nameof(ProfileModCountLabel), nameof(ProfileManageMods), nameof(ProfileJavaRam),
        nameof(ProfilePlay), nameof(ProfileVersions), nameof(ProfileMods),
        nameof(ProfileVersionsSubtitle), nameof(ProfileModsSubtitle),
        nameof(NewsTitle), nameof(NewsSubtitle), nameof(NewsLoading),
        nameof(NewsLoadError), nameof(NewsRetry), nameof(NewsEmpty),
        nameof(NewsWatch), nameof(NewsMore), nameof(NewsClose), nameof(NewsPlayerError),
        nameof(VersionsTitle), nameof(VersionsLoading), nameof(VersionsLoaded),
        nameof(VersionsNoneFound), nameof(VersionsLoadError), nameof(VersionsSearchPlaceholder),
        nameof(VersionsRefresh), nameof(VersionsInstall), nameof(VersionsRelease),
        nameof(VersionsSnapshot), nameof(VersionsInstalled), nameof(VersionsDefault),
        nameof(VersionsSelectFirst), nameof(VersionsInstallSuccess), nameof(VersionsInstallFailed),
        nameof(VersionsFilterAll),
        nameof(ModsTitle), nameof(ModsOpenFolder), nameof(ModsRefresh), nameof(ModsNoneFound),
        nameof(ModsEmptyText), nameof(ModsEmptyShort), nameof(ModsCount), nameof(ModsActive),
        nameof(ModsDisabled), nameof(ModsEnabled), nameof(ModsDisabledMsg),
        nameof(ModsError),
        nameof(ModsSectionInstalled), nameof(ModsSectionModrinth), nameof(ModsSectionCurseForge),
        nameof(ModsCurseForgeIntro), nameof(ModsCurseForgeNoApiKey),
        nameof(ModsCurseForgeDisabled), nameof(ModsCurseForgeDisabledHint),
        nameof(ModsGameVersionLabel), nameof(ModsLoaderLabel),
        nameof(ModsLoaderFabric), nameof(ModsLoaderForge), nameof(ModsLoaderNeoForge),
        nameof(ModsLoaderQuilt), nameof(ModsLoaderLiteLoader),
        nameof(ModsSearchPlaceholder), nameof(ModsSearchButton),
        nameof(ModsInstall), nameof(ModsInstalling), nameof(ModsInstalledBadge),
        nameof(ModsUpdate), nameof(ModsUpdating), nameof(ModsUninstall),
        nameof(ModsNoVersionSelected), nameof(ModsNoResults), nameof(ModsSearchStartHint), nameof(ModsSearchError),
        nameof(ModsBrowseButton), nameof(ModsModrinthIntro),
        nameof(ModsInstallSuccess), nameof(ModsInstallNoVersion), nameof(ModsInstallAlready),
        nameof(ModsInstallError), nameof(ModsUpdateSuccess), nameof(ModsUpdateError),
        nameof(ModsUninstalled),
        nameof(ModsUnknownMod), nameof(ModsUnknownVersion), nameof(ModsUnknownLoader),
        nameof(ModsIsLoading), nameof(ModsGoToVersions),
        nameof(SettingsTitle), nameof(SettingsSubtitle), nameof(SettingsProfile),
        nameof(SettingsUsername), nameof(SettingsUsernamePlaceholder),
        nameof(SettingsMicrosoftAccount), nameof(SettingsMicrosoftAccountHint),
        nameof(SettingsMicrosoftLoginButton), nameof(SettingsMicrosoftLoginNoClientId),
        nameof(SettingsMicrosoftLoginOpeningBrowser), nameof(SettingsMicrosoftLoginWaiting),
        nameof(SettingsMicrosoftLoginChecking), nameof(SettingsMicrosoftLoggedInAs),
        nameof(SettingsMicrosoftLoginCancelled), nameof(SettingsMicrosoftLoginError),
        nameof(SettingsMicrosoftLoginRestoring),
        nameof(SettingsJavaRuntime),
        nameof(SettingsJavaPathHint), nameof(SettingsBrowse), nameof(SettingsJavaFound),
        nameof(SettingsJavaNotFound), nameof(SettingsDetectedRuntimes), nameof(SettingsRescan),
        nameof(SettingsUse), nameof(SettingsGameData), nameof(SettingsGameDataHint),
        nameof(SettingsGameDataSubfolders), nameof(SettingsOpenExplorer), nameof(SettingsResetDefault),
        nameof(SettingsRam), nameof(SettingsRamHint), nameof(SettingsDesign), nameof(SettingsTheme),
        nameof(SettingsSound), nameof(SettingsSoundHint), nameof(SettingsSoundVolume),
        nameof(SettingsDark), nameof(SettingsLight), nameof(SettingsAccent),
        nameof(SettingsTransparency), nameof(SettingsTransparencyNone),
        nameof(SettingsTransparencyLight), nameof(SettingsTransparencyStrong),
        nameof(SettingsBackground), nameof(SettingsBackgroundChoose),
        nameof(SettingsBackgroundReset), nameof(SettingsLanguage),
        nameof(SettingsGerman), nameof(SettingsEnglish), nameof(SettingsSave), nameof(SettingsSaved),
        nameof(SettingsSkinCapes), nameof(SettingsSkinCapesHint), nameof(SettingsSkinCapesRefresh),
        nameof(SettingsSkinCapesCount),
        nameof(SettingsBack),
        nameof(SettingsCategoryDesign), nameof(SettingsCategoryDesignSubtitle),
        nameof(SettingsCategoryUi), nameof(SettingsCategoryUiSubtitle),
        nameof(SettingsCategoryMinecraft), nameof(SettingsCategoryMinecraftSubtitle),
        nameof(SettingsCategoryMods), nameof(SettingsCategoryModsSubtitle),
        nameof(SettingsCategoryAccount), nameof(SettingsCategoryAccountSubtitle),
        nameof(SettingsCategoryGeneral), nameof(SettingsCategoryGeneralSubtitle),
        nameof(SettingsModsLoader), nameof(SettingsModsLoaderHint),
        nameof(SettingsModsFolder), nameof(SettingsModsFolderHint),
        nameof(SettingsCurseForgeApiKey), nameof(SettingsCurseForgeApiKeyHint), nameof(SettingsCurseForgeApiKeyInfo),
        nameof(SettingsUsernameHint),
        nameof(SettingsUninstallTitle), nameof(SettingsUninstallConfirm),
        nameof(SettingsUninstallYes), nameof(SettingsUninstallNo),
        nameof(SettingsUninstallButton), nameof(SettingsUninstallHint),
        nameof(BackgroundEnabled), nameof(BackgroundEnabledHint),
        nameof(BackgroundImage), nameof(BackgroundImageHint),
        nameof(BackgroundPreview), nameof(BackgroundPreviewOff),
        nameof(BackgroundElements), nameof(BackgroundAnimation),
        nameof(BackgroundKind), nameof(BackgroundColor),
        nameof(BackgroundCount), nameof(BackgroundSize), nameof(BackgroundDensity),
        nameof(BackgroundOpacity), nameof(BackgroundSpacing),
        nameof(BackgroundAnimate), nameof(BackgroundSpeed),
        nameof(BackgroundRotate), nameof(BackgroundRotationSpeed),
        nameof(BackgroundIntensity), nameof(BackgroundReset),
        nameof(BackgroundKindPawn), nameof(BackgroundKindKnight), nameof(BackgroundKindBishop),
        nameof(BackgroundKindRook), nameof(BackgroundKindQueen), nameof(BackgroundKindKing),
        nameof(BackgroundKindCircle), nameof(BackgroundKindSquare), nameof(BackgroundKindTriangle),
        nameof(BackgroundKindDiamond), nameof(BackgroundKindHexagon), nameof(BackgroundKindRing),
        nameof(BackgroundKindStar), nameof(BackgroundKindParticle),
        nameof(AccentGreen), nameof(AccentRed), nameof(AccentBlue), nameof(AccentOrange),
        nameof(AccentPurple), nameof(AccentCyan), nameof(AccentYellow), nameof(AccentWhite),
        nameof(AccentBlack), nameof(AccentPink), nameof(AccentSilver),
        nameof(JavaUnknownVersion),
    ];

    private static readonly Dictionary<string, Translation> Strings = new()
    {
        // Navigation
        ["Nav.Start"] = new("Start", "Home"),
        ["Nav.Profile"] = new("Profile", "Profiles"),
        ["Nav.Settings"] = new("Einstellungen", "Settings"),
        ["Nav.News"] = new("News", "News"),

        // Branding
        ["Branding.Subtitle"] = new("Dein Minecraft Launcher", "Your Minecraft launcher"),

        // Update
        ["Update.Available"] = new("Update verfügbar: v{0}", "Update available: v{0}"),
        ["Update.Install"] = new("Aktualisieren", "Update"),
        ["Update.Later"] = new("Später", "Later"),
        ["Update.Failed"] = new("Update fehlgeschlagen: {0}", "Update failed: {0}"),

        // Home
        ["Home.WelcomeBack"] = new("Willkommen zurück", "Welcome back"),
        ["Home.NoVersionSelected"] = new("Noch keine Version gewählt", "No version selected yet"),
        ["Home.Play"] = new("Spielen", "Play"),
        ["Home.ActiveProfile"] = new("Aktives Profil", "Active profile"),
        ["Home.ManageProfiles"] = new("Profile verwalten", "Manage profiles"),
        ["Home.Ready"] = new("Bereit.", "Ready."),
        ["Home.DirectLaunchHint"] = new(
            "„Spielen“ startet Minecraft: Java Edition direkt über deine installierte Version – ohne den offiziellen Launcher. Die Anmeldung läuft über dein eigenes Microsoft-Konto (Einstellungen → Microsoft-Konto).",
            "“Play” launches Minecraft: Java Edition directly from your installed version – without the official launcher. Sign-in uses your own Microsoft account (Settings → Microsoft account)."),
        ["Home.StartingGame"] = new("Minecraft wird gestartet …", "Starting Minecraft …"),
        ["Home.GameStarted"] = new("Minecraft {0} wurde gestartet.", "Minecraft {0} has been started."),
        ["Home.NotSignedIn"] = new(
            "Bitte melde dich zuerst in den Einstellungen mit deinem Microsoft-Konto an.",
            "Please sign in with your Microsoft account in the settings first."),
        ["Home.VersionNotInstalled"] = new(
            "Die Version {0} ist nicht installiert. Bitte zuerst unter „Versionen“ installieren.",
            "Version {0} is not installed. Please install it under “Versions” first."),
        ["Home.VersionDataError"] = new(
            "Die Versionsdaten von {0} sind beschädigt oder unvollständig.",
            "The version data of {0} is corrupted or incomplete."),
        ["Home.JavaNotFound"] = new(
            "Keine passende Java-Runtime gefunden (benötigt: Java {0}). Bitte in den Einstellungen einen Java-Pfad angeben.",
            "No matching Java runtime found (required: Java {0}). Please specify a Java path in the settings."),
        ["Home.MissingLibraries"] = new(
            "Für {0} fehlen {1} benötigte Datei(en) (Libraries/Assets). Bitte die Version neu installieren.",
            "{1} required file(s) (libraries/assets) are missing for {0}. Please reinstall the version."),
        ["Home.GameLaunchFailed"] = new("Minecraft konnte nicht gestartet werden: {0}", "Minecraft could not be started: {0}"),
        ["Home.InstalledVersions"] = new("Installierte Versionen", "Installed versions"),
        ["Home.ModsInFolder"] = new("Mods im Ordner", "Mods in folder"),
        ["Home.ActivePlayer"] = new("Aktiver Spielername", "Active player name"),
        ["Home.AboutTitle"] = new("Über Kulka Client", "About Kulka Client"),
        ["Home.AboutText"] = new(
            "Kulka Client ist dein eigener Minecraft Launcher. Der Prototyp lädt die Versionsliste von Mojang, installiert ausgewählte Versionen, verwaltet Mods und Einstellungen und startet Minecraft direkt über deine Microsoft-Anmeldung – ganz ohne den offiziellen Launcher. Weitere Funktionen sind bereits vorbereitet.",
            "Kulka Client is your own Minecraft launcher. The prototype loads the version list from Mojang, installs selected versions, manages mods and settings and launches Minecraft directly with your Microsoft sign-in – without the official launcher. More features are already prepared."),
        ["Home.Profile"] = new("Profil", "Profile"),
        ["Home.ProfileStandard"] = new("Standard", "Default"),
        ["Home.ProfileNew"] = new("Neu", "New"),
        ["Home.ProfileRename"] = new("Umbenennen", "Rename"),
        ["Home.ProfileDuplicate"] = new("Duplizieren", "Duplicate"),
        ["Home.ProfileDelete"] = new("Löschen", "Delete"),
        ["Home.ProfileDeleteConfirm"] = new("Wirklich löschen?", "Delete?"),
        ["Home.ProfileDeleteHint"] = new(
            "Klicke erneut auf „Wirklich löschen?“, um „{0}“ endgültig zu löschen.",
            "Click “Delete?” again to permanently delete “{0}”."),
        ["Home.ProfileNamePlaceholder"] = new("Name des Profils", "Profile name"),
        ["Home.ProfileOk"] = new("Übernehmen", "OK"),
        ["Home.ProfileCancel"] = new("Abbrechen", "Cancel"),
        ["Home.ProfileHint"] = new(
            "Jedes Profil hat seine eigene Minecraft-Version, seinen Mod-Loader, Java-Einstellungen und einen eigenen Mods-Ordner.",
            "Each profile has its own Minecraft version, mod loader, Java settings and a separate mods folder."),
        ["Home.ProfileCreated"] = new("Profil erstellt.", "Profile created."),
        ["Home.ProfileRenamed"] = new("Profil umbenannt.", "Profile renamed."),
        ["Home.ProfileDeleted"] = new("Profil „{0}“ gelöscht.", "Profile “{0}” deleted."),
        ["Home.ProfileDuplicated"] = new("Profil dupliziert.", "Profile duplicated."),
        ["Home.ProfileNameExists"] = new(
            "Ein Profil mit diesem Namen existiert bereits.",
            "A profile with this name already exists."),
        ["Home.ProfileEmptyName"] = new(
            "Bitte einen Namen für das Profil eingeben.",
            "Please enter a name for the profile."),
        ["Home.ProfileFolderConflict"] = new(
            "Dieser Name erzeugt denselben Ordner wie ein anderes Profil – bitte einen anderen Namen wählen.",
            "This name would use the same folder as another profile – please choose a different name."),

        // Profile
        ["Profile.Title"] = new("Profile", "Profiles"),
        ["Profile.Subtitle"] = new(
            "Profil auswählen, Minecraft-Version und Mods einstellen",
            "Select a profile, set the Minecraft version and mods"),
        ["Profile.Back"] = new("Zurück", "Back"),
        ["Profile.Profiles"] = new("Profile", "Profiles"),
        ["Profile.NewProfile"] = new("+ Neues Profil", "+ New profile"),
        ["Profile.ActiveProfile"] = new("Aktives Profil", "Active profile"),
        ["Profile.VersionLabel"] = new("Minecraft-Version", "Minecraft version"),
        ["Profile.ManageVersions"] = new("Versionen verwalten", "Manage versions"),
        ["Profile.LoaderLabel"] = new("Loader", "Loader"),
        ["Profile.ModCountLabel"] = new("Mods", "Mods"),
        ["Profile.ModCount"] = new("{0} installiert", "{0} installed"),
        ["Profile.ManageMods"] = new("Mods verwalten", "Manage mods"),
        ["Profile.JavaRam"] = new("Java & Arbeitsspeicher", "Java & memory"),
        ["Profile.Play"] = new("Spielen", "Play"),
        ["Profile.Versions"] = new("Versionen", "Versions"),
        ["Profile.Mods"] = new("Mods", "Mods"),
        ["Profile.VersionsSubtitle"] = new(
            "Minecraft-Versionen durchsuchen, installieren und als aktive Version wählen",
            "Browse and install Minecraft versions and pick the active version"),
        ["Profile.ModsSubtitle"] = new(
            "Mods durchsuchen, installieren, aktualisieren und verwalten",
            "Browse, install, update and manage mods"),

        // News
        ["News.Title"] = new("News", "News"),
        ["News.Subtitle"] = new("Neue Videos von @ANG3L0WW", "New videos from @ANG3L0WW"),
        ["News.Loading"] = new("Lade News …", "Loading news …"),
        ["News.LoadError"] = new(
            "News konnten momentan nicht geladen werden.",
            "News could not be loaded right now."),
        ["News.Retry"] = new("Erneut versuchen", "Try again"),
        ["News.Empty"] = new("Noch keine Videos vorhanden.", "No videos yet."),
        ["News.Watch"] = new("Anschauen", "Watch"),
        ["News.More"] = new("Weitere Videos", "More videos"),
        ["News.JustNow"] = new("gerade eben", "just now"),
        ["News.MinAgo"] = new("vor {0} Min.", "{0} min ago"),
        ["News.HourAgo"] = new("vor {0} Std.", "{0} hrs ago"),
        ["News.DaysAgo"] = new("vor {0} Tagen", "{0} days ago"),
        ["News.Close"] = new("Schließen", "Close"),
        ["News.PlayerError"] = new(
            "Der eingebettete YouTube-Player ist auf diesem Gerät nicht verfügbar.",
            "The embedded YouTube player is not available on this device."),

        // Versions
        ["Versions.Title"] = new("Minecraft-Versionen", "Minecraft versions"),
        ["Versions.Loading"] = new("Lade Versionsliste von Mojang …", "Loading version list from Mojang …"),
        ["Versions.Loaded"] = new("{0} Versionen geladen.", "{0} versions loaded."),
        ["Versions.NoneFound"] = new("Keine Versionen gefunden.", "No versions found."),
        ["Versions.LoadError"] = new("Fehler beim Laden: {0}", "Error while loading: {0}"),
        ["Versions.SearchPlaceholder"] = new("Suche nach Version …", "Search for version …"),
        ["Versions.Refresh"] = new("Aktualisieren", "Refresh"),
        ["Versions.Install"] = new("Installieren", "Install"),
        ["Versions.Release"] = new("Release", "Release"),
        ["Versions.Snapshot"] = new("Snapshot", "Snapshot"),
        ["Versions.Installed"] = new("Installiert", "Installed"),
        ["Versions.Default"] = new("Standard", "Default"),
        ["Versions.SelectFirst"] = new(
            "Bitte zuerst eine Version auswählen.",
            "Please select a version first."),
        ["Versions.InstallSuccess"] = new(
            "Version {0} wurde erfolgreich installiert.",
            "Version {0} was installed successfully."),
        ["Versions.InstallFailed"] = new("Installation fehlgeschlagen: {0}", "Installation failed: {0}"),
        ["Versions.FilterAll"] = new("Alle", "All"),

        // Mods
        ["Mods.Title"] = new("Mods", "Mods"),
        ["Mods.OpenFolder"] = new("Ordner öffnen", "Open folder"),
        ["Mods.Refresh"] = new("Aktualisieren", "Refresh"),
        ["Mods.NoneFound"] = new("Keine Mods gefunden.", "No mods found."),
        ["Mods.EmptyText"] = new(
            "Lege .jar-Dateien in den Mods-Ordner und klicke auf „Aktualisieren“. Deaktivierte Mods werden mit .disabled markiert.",
            "Place .jar files in the mods folder and click “Refresh”. Disabled mods are marked with .disabled."),
        ["Mods.EmptyShort"] = new(
            "Keine Mods gefunden. Lege .jar-Dateien in den Mods-Ordner.",
            "No mods found. Place .jar files in the mods folder."),
        ["Mods.Count"] = new("{0} Mod(s) gefunden.", "{0} mod(s) found."),
        ["Mods.Active"] = new("Aktiv", "Active"),
        ["Mods.Disabled"] = new("Deaktiviert", "Disabled"),
        ["Mods.Enabled"] = new("{0} wurde aktiviert.", "{0} was enabled."),
        ["Mods.DisabledMsg"] = new("{0} wurde deaktiviert.", "{0} was disabled."),
        ["Mods.Error"] = new("Fehler: {0}", "Error: {0}"),
        ["Mods.SectionInstalled"] = new("Installierte Mods", "Installed mods"),
        ["Mods.SectionModrinth"] = new("Modrinth", "Modrinth"),
        ["Mods.SectionCurseForge"] = new("CurseForge", "CurseForge"),
        ["Mods.CurseForgeIntro"] = new(
            "CurseForge ist direkt eingebunden – echte Suchergebnisse und Downloads über die offizielle API.",
            "CurseForge is embedded directly – real results and downloads via the official API."),
        ["Mods.CurseForgeNoApiKey"] = new(
            "Kein CurseForge-API-Schluessel konfiguriert. Bitte in den Einstellungen einen API-Schluessel eintragen.",
            "No CurseForge API key configured. Please enter an API key in the settings."),
        ["Mods.CurseForgeDisabled"] = new(
            "CurseForge ist derzeit nicht verfuegbar.",
            "CurseForge is currently not available."),
        ["Mods.CurseForgeDisabledHint"] = new(
            "Die CurseForge-Integration wird spaeter aktiviert. Du kannst weiterhin Modrinth nutzen.",
            "The CurseForge integration will be activated later. You can continue using Modrinth."),
        ["Mods.GameVersionLabel"] = new("Minecraft-Version", "Minecraft version"),
        ["Mods.LoaderLabel"] = new("Loader", "Loader"),
        ["Mods.LoaderFabric"] = new("Fabric", "Fabric"),
        ["Mods.LoaderForge"] = new("Forge", "Forge"),
        ["Mods.LoaderNeoForge"] = new("NeoForge", "NeoForge"),
        ["Mods.LoaderQuilt"] = new("Quilt", "Quilt"),
        ["Mods.LoaderLiteLoader"] = new("LiteLoader", "LiteLoader"),
        ["Mods.SearchPlaceholder"] = new("Nach Mod suchen …", "Search for a mod …"),
        ["Mods.SearchButton"] = new("Suchen", "Search"),
        ["Mods.Install"] = new("Installieren", "Install"),
        ["Mods.Installing"] = new("Installiere …", "Installing …"),
        ["Mods.InstalledBadge"] = new("Installiert", "Installed"),
        ["Mods.Update"] = new("Update", "Update"),
        ["Mods.Updating"] = new("Aktualisiere …", "Updating …"),
        ["Mods.Uninstall"] = new("Deinstallieren", "Uninstall"),
        ["Mods.NoVersionSelected"] = new(
            "Bitte wähle zuerst unter „Versionen“ eine Minecraft-Version aus.",
            "Please select a Minecraft version under “Versions” first."),
        ["Mods.NoResults"] = new("Keine Ergebnisse gefunden.", "No results found."),
        ["Mods.SearchStartHint"] = new(
            "Suche nach einem Mod – die Ergebnisse kommen live von Modrinth, nichts ist vorgetäuscht.",
            "Search for a mod – results come live from Modrinth, nothing is faked."),
        ["Mods.SearchError"] = new("Suche fehlgeschlagen: {0}", "Search failed: {0}"),
        ["Mods.BrowseButton"] = new("Beliebte Mods laden", "Load popular mods"),
        ["Mods.ModrinthIntro"] = new(
            "Modrinth ist direkt eingebunden – echte Suchergebnisse und Downloads über die offizielle API, kein Browser.",
            "Modrinth is embedded directly – real results and downloads via the official API, no browser."),
        ["Mods.InstallSuccess"] = new("{0} wurde installiert.", "{0} was installed."),
        ["Mods.InstallNoVersion"] = new(
            "Für {0} gibt es keine Version für Minecraft {1} mit {2}.",
            "There is no version of {0} for Minecraft {1} with {2}."),
        ["Mods.InstallAlready"] = new("{0} ist bereits installiert.", "{0} is already installed."),
        ["Mods.InstallError"] = new("Installation fehlgeschlagen: {0}", "Installation failed: {0}"),
        ["Mods.UpdateSuccess"] = new("{0} wurde aktualisiert.", "{0} was updated."),
        ["Mods.UpdateError"] = new("Update fehlgeschlagen: {0}", "Update failed: {0}"),
        ["Mods.UpdateAvailable"] = new("Update verfügbar: {0}", "Update available: {0}"),
        ["Mods.Uninstalled"] = new("{0} wurde deinstalliert.", "{0} was uninstalled."),
        ["Mods.UnknownMod"] = new("Unbekannte Mod", "Unknown mod"),
        ["Mods.UnknownVersion"] = new("Unbekannte Version", "Unknown version"),
        ["Mods.UnknownLoader"] = new("Unbekannter Loader", "Unknown loader"),
        ["Mods.Downloads"] = new("Downloads: {0}", "Downloads: {0}"),
        ["Mods.Follows"] = new("Follower: {0}", "Followers: {0}"),
        ["Mods.InstalledWithDeps"] = new(
            "{0} installiert ({1} Dateien inkl. Abhängigkeiten).",
            "{0} installed ({1} files including dependencies)."),
        ["Mods.InstallWithMissingDeps"] = new(
            "{0} wurde installiert. Hinweis: {1} benötigte Abhängigkeit(en) wurden nicht gefunden ({2}).",
            "{0} was installed. Note: {1} required dependenc(y/ies) were not found ({2})."),
        ["Mods.UpdateWithMissingDeps"] = new(
            "{0} wurde aktualisiert. Hinweis: {1} benötigte Abhängigkeit(en) wurden nicht gefunden ({2}).",
            "{0} was updated. Note: {1} required dependenc(y/ies) were not found ({2})."),
        ["Mods.GoToVersions"] = new("Zur Versionsauswahl", "Go to versions"),
        ["Mods.IsLoading"] = new("Lade …", "Loading …"),

        // Settings
        ["Settings.Title"] = new("Einstellungen", "Settings"),
        ["Settings.Subtitle"] = new("Profil, Java, Spieldaten und Design", "Profile, Java, game data and design"),
        ["Settings.Profile"] = new("Profil", "Profile"),
        ["Settings.Username"] = new("Spielername", "Player name"),
        ["Settings.UsernamePlaceholder"] = new("Dein Minecraft-Spielername", "Your Minecraft player name"),
        ["Settings.MicrosoftAccount"] = new("Microsoft-Konto", "Microsoft account"),
        ["Settings.MicrosoftAccountHint"] = new(
            "Melde dich mit deinem Microsoft-Konto an, um Minecraft zu starten. Die Anmeldung läuft über den Browser (Authorization-Code + PKCE) – es werden keine Passwörter oder Tokens aus dem offiziellen Launcher ausgelesen.",
            "Sign in with your Microsoft account to launch Minecraft. Sign-in happens via browser (authorization code + PKCE) – no passwords or tokens are read from the official launcher."),
        ["Settings.MicrosoftLoginButton"] = new("Mit Microsoft anmelden", "Sign in with Microsoft"),
        ["Settings.MicrosoftLoginNoClientId"] = new(
            "Die Microsoft-Anmeldung ist vorübergehend nicht verfügbar.",
            "Microsoft sign-in is temporarily unavailable."),
        ["Settings.MicrosoftLoginOpeningBrowser"] = new(
            "Browser wird geöffnet …", "Opening browser …"),
        ["Settings.MicrosoftLoginWaiting"] = new(
            "Warte auf Microsoft-Anmeldung …", "Waiting for Microsoft sign-in …"),
        ["Settings.MicrosoftLoginChecking"] = new(
            "Minecraft-Konto wird überprüft …", "Checking Minecraft account …"),
        ["Settings.MicrosoftLoggedInAs"] = new("Angemeldet als {0}", "Signed in as {0}"),
        ["Settings.MicrosoftLoginCancelled"] = new(
            "Anmeldung abgebrochen oder Zeitüberschreitung.", "Sign-in cancelled or timed out."),
        ["Settings.MicrosoftLoginError"] = new(
            "Anmeldung fehlgeschlagen: {0}", "Sign-in failed: {0}"),
        ["Settings.MicrosoftLoginRestoring"] = new(
            "Gespeicherte Anmeldung wird geprüft …", "Checking saved sign-in …"),
        ["Settings.JavaRuntime"] = new("Java-Runtime", "Java runtime"),
        ["Settings.JavaPathHint"] = new(
            "Pfad zu java.exe – leer lassen für automatische Suche",
            "Path to java.exe – leave empty for automatic detection"),
        ["Settings.Browse"] = new("Durchsuchen …", "Browse …"),
        ["Settings.JavaFound"] = new("Java gefunden: {0}", "Java found: {0}"),
        ["Settings.JavaNotFound"] = new(
            "Keine Java-Runtime automatisch gefunden.",
            "No Java runtime found automatically."),
        ["Settings.DetectedRuntimes"] = new("Erkannte Java-Versionen", "Detected Java versions"),
        ["Settings.Rescan"] = new("Neu suchen", "Rescan"),
        ["Settings.Use"] = new("Verwenden", "Use"),
        ["Settings.GameData"] = new("Spieldaten", "Game data"),
        ["Settings.GameDataHint"] = new(
            "Verzeichnis für Versionen, Mods und Logs",
            "Directory for versions, mods and logs"),
        ["Settings.GameDataSubfolders"] = new(
            "Enthält die Unterordner versions, mods und logs.",
            "Contains the versions, mods and logs subfolders."),
        ["Settings.OpenExplorer"] = new("Im Explorer öffnen", "Open in Explorer"),
        ["Settings.ResetDefault"] = new("Standardpfad", "Default path"),
        ["Settings.Ram"] = new("Arbeitsspeicher (RAM)", "Memory (RAM)"),
        ["Settings.RamHint"] = new("Empfohlen: 2048 – 8192 MB", "Recommended: 2048 – 8192 MB"),
        ["Settings.Sound"] = new("UI-Sounds", "UI sounds"),
        ["Settings.SoundHint"] = new(
            "Dezenter Klick-Sound bei Schaltflächen und Navigation.",
            "Subtle click sound for buttons and navigation."),
        ["Settings.SoundVolume"] = new("Lautstärke", "Volume"),
        ["Settings.Design"] = new("Design", "Design"),
        ["Settings.Theme"] = new("Theme", "Theme"),
        ["Settings.Dark"] = new("Dunkel", "Dark"),
        ["Settings.Light"] = new("Hell", "Light"),
        ["Settings.Accent"] = new("Akzentfarbe", "Accent color"),
        ["Settings.Transparency"] = new("Transparenz", "Transparency"),
        ["Settings.TransparencyNone"] = new("Aus", "Off"),
        ["Settings.TransparencyLight"] = new("Leicht", "Light"),
        ["Settings.TransparencyStrong"] = new("Stärker", "Strong"),
        ["Settings.Background"] = new("Hintergrund", "Background"),
        ["Settings.BackgroundChoose"] = new("Bild auswählen …", "Choose image …"),
        ["Settings.BackgroundReset"] = new("Zurücksetzen", "Reset"),
        ["Settings.Language"] = new("Sprache", "Language"),
        ["Settings.German"] = new("Deutsch", "German"),
        ["Settings.English"] = new("English", "English"),
        ["Settings.Save"] = new("Einstellungen speichern", "Save settings"),
        ["Settings.Saved"] = new("Einstellungen wurden gespeichert.", "Settings have been saved."),
        ["Settings.SkinCapes"] = new("Skins & Capes", "Skins & capes"),
        ["Settings.SkinCapesHint"] = new(
            "Lokale Skins und Capes werden durchsucht. Die Online-Anbindung an Mojang folgt in einem späteren Schritt und wird hier dann automatisch aktualisiert.",
            "Local skins and capes are scanned. Online integration with Mojang will follow in a later step and will then be updated automatically here."),
        ["Settings.SkinCapesRefresh"] = new("Aktualisieren", "Refresh"),
        ["Settings.SkinCapesCount"] = new(
            "{0} Skin(s), {1} Cape(s) lokal gefunden.",
            "{0} skin(s), {1} cape(s) found locally."),
        ["Settings.Back"] = new("Zurück", "Back"),
        ["Settings.CategoryDesign"] = new("Design", "Design"),
        ["Settings.CategoryDesignSubtitle"] = new(
            "Theme, Akzentfarbe, Transparenz & Hintergrund",
            "Theme, accent color, transparency & background"),
        ["Settings.CategoryUi"] = new("Oberfläche", "Interface"),
        ["Settings.CategoryUiSubtitle"] = new(
            "Sprache, UI-Sounds, Skins & Capes",
            "Language, UI sounds, skins & capes"),
        ["Settings.CategoryMinecraft"] = new("Minecraft", "Minecraft"),
        ["Settings.CategoryMinecraftSubtitle"] = new(
            "Java-Runtime, Spieldaten & Arbeitsspeicher",
            "Java runtime, game data & memory"),
        ["Settings.CategoryMods"] = new("Mods", "Mods"),
        ["Settings.CategoryModsSubtitle"] = new(
            "Mod-Loader & Mods-Ordner",
            "Mod loader & mods folder"),
        ["Settings.CategoryAccount"] = new("Konto", "Account"),
        ["Settings.CategoryAccountSubtitle"] = new(
            "Microsoft-Konto & Anmeldung",
            "Microsoft account & sign-in"),
        ["Settings.CategoryGeneral"] = new("Allgemein", "General"),
        ["Settings.CategoryGeneralSubtitle"] = new(
            "Spielername & Grundlagen",
            "Player name & basics"),
        ["Settings.ModsLoader"] = new("Mod-Loader", "Mod loader"),
        ["Settings.ModsLoaderHint"] = new(
            "Der Loader bestimmt, welche Mods gesucht und installiert werden. Er gehört zum aktiven Profil.",
            "The loader decides which mods are searched and installed. It belongs to the active profile."),
        ["Settings.ModsFolder"] = new("Mods-Ordner", "Mods folder"),
        ["Settings.ModsFolderHint"] = new(
            "Jedes Profil hat seinen eigenen Mods-Ordner – so bleiben die Mods der Profile getrennt.",
            "Each profile has its own mods folder – so the mods of your profiles stay separate."),
        ["Settings.CurseForgeApiKey"] = new("CurseForge API-Schlüssel", "CurseForge API key"),
        ["Settings.CurseForgeApiKeyHint"] = new(
            "API-Schlüssel für die Modsuche auf CurseForge. Kostenloser $2a$-Schlüssel über die CurseForge-API-Website.",
            "API key for searching mods on CurseForge. Free $2a$ key via the CurseForge API website."),
        ["Settings.CurseForgeApiKeyInfo"] = new(
            "Den Schluessel auf https://console.curseforge.com/ anfordern (API Tools -> API Key).",
            "Request your key at https://console.curseforge.com/ (API Tools -> API Key)."),
        ["Settings.UsernameHint"] = new(
            "Der Spielername erscheint auf der Startseite und wird beim Spielstart verwendet.",
            "The player name appears on the home page and is used when launching the game."),

        // Uninstall
        ["Settings.UninstallTitle"] = new("Kulka Client deinstallieren", "Uninstall Kulka Client"),
        ["Settings.UninstallConfirm"] = new(
            "Möchtest du Kulka Client wirklich deinstallieren? Alle Einstellungen und Profile werden entfernt.",
            "Do you really want to uninstall Kulka Client? All settings and profiles will be removed."),
        ["Settings.UninstallYes"] = new("Deinstallieren", "Uninstall"),
        ["Settings.UninstallNo"] = new("Abbrechen", "Cancel"),
        ["Settings.UninstallButton"] = new("Kulka Client deinstallieren", "Uninstall Kulka Client"),
        ["Settings.UninstallHint"] = new(
            "Entfernt Kulka Client komplett von deinem Computer.",
            "Completely removes Kulka Client from your computer."),

        // Hintergrund-Editor
        ["Background.Enabled"] = new("Hintergrund aktivieren", "Enable background"),
        ["Background.EnabledHint"] = new(
            "Schaltet den kompletten dekorativen Hintergrund an oder aus. Das eigene Bild (optional) wird nur bei aktivem Hintergrund gezeigt.",
            "Turns the entire decorative background on or off. Your own image (optional) is only shown while the background is enabled."),
        ["Background.Image"] = new("Hintergrundbild", "Background image"),
        ["Background.ImageHint"] = new(
            "Optional ein eigenes Bild hinter der Oberfläche – es wird dezent eingeblendet und niemals von der Bedienung überdeckt.",
            "Optionally an image behind the UI – it is shown subtly and never blocks the controls."),
        ["Background.Preview"] = new("Vorschau", "Preview"),
        ["Background.PreviewOff"] = new("Hintergrund ist deaktiviert.", "Background is disabled."),
        ["Background.Elements"] = new("Elemente", "Elements"),
        ["Background.Animation"] = new("Animation", "Animation"),
        ["Background.Kind"] = new("Elementtypen", "Element types"),
        ["Background.Color"] = new("Farbe", "Color"),
        ["Background.Count"] = new("Anzahl", "Count"),
        ["Background.Size"] = new("Größe", "Size"),
        ["Background.Density"] = new("Dichte", "Density"),
        ["Background.Opacity"] = new("Transparenz", "Opacity"),
        ["Background.Spacing"] = new("Abstand / Verteilung", "Spacing / distribution"),
        ["Background.Animate"] = new("Animation aktivieren", "Enable animation"),
        ["Background.Speed"] = new("Geschwindigkeit", "Speed"),
        ["Background.Rotate"] = new("Rotation", "Rotation"),
        ["Background.RotationSpeed"] = new("Rotationsgeschwindigkeit", "Rotation speed"),
        ["Background.Intensity"] = new("Intensität", "Intensity"),
        ["Background.Reset"] = new("Standard wiederherstellen", "Restore defaults"),

        ["Background.Kind.Pawn"] = new("Bauer", "Pawn"),
        ["Background.Kind.Knight"] = new("Springer", "Knight"),
        ["Background.Kind.Bishop"] = new("Läufer", "Bishop"),
        ["Background.Kind.Rook"] = new("Turm", "Rook"),
        ["Background.Kind.Queen"] = new("Dame", "Queen"),
        ["Background.Kind.King"] = new("König", "King"),
        ["Background.Kind.Circle"] = new("Kreis", "Circle"),
        ["Background.Kind.Square"] = new("Quadrat", "Square"),
        ["Background.Kind.Triangle"] = new("Dreieck", "Triangle"),
        ["Background.Kind.Diamond"] = new("Raute", "Diamond"),
        ["Background.Kind.Hexagon"] = new("Sechseck", "Hexagon"),
        ["Background.Kind.Ring"] = new("Ring", "Ring"),
        ["Background.Kind.Star"] = new("Stern", "Star"),
        ["Background.Kind.Particle"] = new("Partikel", "Particle"),

        // Akzentfarben
        ["Accent.Green"] = new("Grün", "Green"),
        ["Accent.Red"] = new("Rot", "Red"),
        ["Accent.Blue"] = new("Blau", "Blue"),
        ["Accent.Orange"] = new("Orange", "Orange"),
        ["Accent.Purple"] = new("Lila", "Purple"),
        ["Accent.Cyan"] = new("Cyan", "Cyan"),
        ["Accent.Yellow"] = new("Gelb", "Yellow"),
        ["Accent.White"] = new("Weiß", "White"),
        ["Accent.Black"] = new("Schwarz", "Black"),
        ["Accent.Pink"] = new("Rosa", "Pink"),
        ["Accent.Silver"] = new("Silber", "Silver"),

        // Diverses
        ["Java.UnknownVersion"] = new("Unbekannte Version", "Unknown version"),
    };
}
