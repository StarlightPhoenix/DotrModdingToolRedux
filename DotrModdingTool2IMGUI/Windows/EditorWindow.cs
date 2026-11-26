using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using DotrModdingTool2IMGUI.ChangelogDiffCheckers;
using ImGuiNET;
using NativeFileDialogSharp;
using Raylib_cs;
using rlImGui_cs;
using Color = System.Drawing.Color;

namespace DotrModdingTool2IMGUI;

public class EditorWindow
{
    EditorContentMode currentMode = EditorContentMode.EnemyEditor;

    ImGuiIOPtr io = ImGui.GetIO();
    ImFontPtr menuBarFont = FontManager.GetFont(FontManager.FontFamily.NotoSansJP, 24);
    ImFontPtr sideBarFont = FontManager.GetFont(FontManager.FontFamily.NotoSansJP, 20);
    ImFontPtr mainFont = FontManager.GetFont(FontManager.FontFamily.NotoSansJP, 28);

    public static bool Disabled = false;
    float disabledTimer = 0;
    float maxDisabledTime = 30;
    private static float buttonDownscaleFactor = 1.2f;

    public static Vector2 AspectRatio
    {
        get { return ImGui.GetWindowSize() / new Vector2(1920 * buttonDownscaleFactor, 1080 * buttonDownscaleFactor); }
    }

    float buttonWidthScaled = 200f;
    float buttonHeightRatio = 100f;
    float buttonSpacingScaled = 10f;


    GeneralEditorWindow _generalEditorWindow;
    CardEditorWindow _cardEditorWindow;
    GameplayPatchesWindow _gameplayPatchesWindow;
    FusionEditorWindow _fusionEditorWindow;
    ImageEditorWindow _imageEditorWindow;
    RandomiserWindow _randomiserWindow;
    MusicEditorWindow _musicEditorWindow;
    StringEditorWindow _stringEditorWindow;
#if DEBUG
    TextEffectDemoWindow _textEffectDemoWindow;
#endif

    // Allows editors, like Map Editor, to get Credits status
    public static bool isCreditsOpen = false;

    //Data Access
    DataAccess dataAccess = DataAccess.Instance;
    public static ImGuiModalPopup _modalPopup = new ImGuiModalPopup();
    public static Action OnIsoLoaded;
    bool reloadStrings = false;

    bool isSavingStrings = false;
    int stringCompressionProgress;


    //Text rendering effects
    TextEffect batzpupCreditEffect = TextEffect.Gradient(new GuiColour(Color.BlueViolet).value, new GuiColour(Color.CornflowerBlue).value) with {
        RangeEnd = 7,
        OutsideColour = new GuiColour(Color.White).value,
        FillWidth = true
    };

    TextEffect sppCreditEffect = new TextEffect
    {
        Source = ColourSource.Gradient,
        Stops = new[]
            { new Vector4(256f / 256f, 72f / 256f, 206f / 256f, 1.00f), new Vector4(218f / 256f, 218f / 256f, 20f / 256f, 1.00f) },
        Speed = 0.480f,
        Spread = 0.078f,
    };
    
    TextEffect woomyEffect = TextEffect.Gradient(GuiColour.LancasterRed.value, GuiColour.YorkistBlue.value) with {
        Wave = WaveShape.Hold,
        Easing = Easing.Smoothstep,
        Speed = 0.25f,
        Spread = 0.06f
    };

    public static void PrintEmbeddedResources()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        string[] resourceNames = assembly.GetManifestResourceNames();
        Console.WriteLine("Embedded Resources:");
        foreach (string resourceName in resourceNames)
        {
            Console.WriteLine(resourceName);
        }
        Console.WriteLine("_________________________");
    }

    public EditorWindow()
    {
        //  PrintEmbeddedResources();

        io.ConfigFlags |= ImGuiConfigFlags.DpiEnableScaleFonts | ImGuiConfigFlags.DpiEnableScaleViewports;

        _generalEditorWindow = new GeneralEditorWindow(FontManager.GetFont(FontManager.FontFamily.NotoSansJP, 26));
        _cardEditorWindow = new CardEditorWindow();
        _gameplayPatchesWindow = new GameplayPatchesWindow();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _musicEditorWindow = new MusicEditorWindow();
        }
        _fusionEditorWindow = new FusionEditorWindow();

        _randomiserWindow = new RandomiserWindow(_generalEditorWindow);
        _stringEditorWindow = new StringEditorWindow();
        _generalEditorWindow.DeckEditorWindow.ViewCardInEditor += ViewCardInEditor;
        _fusionEditorWindow.Init(_generalEditorWindow.DeckEditorWindow.ViewCardInEditor);
        _imageEditorWindow = new ImageEditorWindow();
#if DEBUG
        _textEffectDemoWindow = new TextEffectDemoWindow(mainFont);
#endif
        Updater.NeedsUpdate += HandleNeedsUpdate;
        //IS here so you can open an iso before the updater gives you a result
        Disabled = true;
        _modalPopup.Show($"Checking for updates",
            "Updater", null, ImGuiModalPopup.ShowType.NoButton);
        Task.Run(async () =>
        {
            await Updater.CheckForUpdates(true);
            Disabled = false;

        });


    }

    void HandleNeedsUpdate(bool needsUpdate, string changes, bool isStartup)
    {
        if (needsUpdate)
        {
            string output = string.Join("\n", changes.Split('\n').Select(line => "- " + line));
            _modalPopup.Show(
                $"An update is available to {Updater.latestVersion} from {Updater.currentVersion}\nChanges:\n{output}\nWould you like to update?",
                "Update", RequestDownload, ImGuiModalPopup.ShowType.YesNo);
        }
        else
        {
            if (!isStartup)
            {
                _modalPopup.Show($"You are up to date, you are on version {Updater.latestVersion}", "Update");
            }
            else
            {
                _modalPopup.Hide();
            }

        }

    }

    void RequestDownload()
    {
        Task.Run(async () =>
        {
            Disabled = true;
            _modalPopup.Show($"Downloading Update.\nThe tool will close, then command prompt will appear swap the files, and re-open the tool",
                "Updater");
            try
            {
                await Updater.CheckForUpdates(true);
            }
            catch (Exception)
            {

            }
            finally
            {
                Disabled = false;
            }
        });

    }

    void ViewCardInEditor(ModdedStringName name)
    {
        currentMode = EditorContentMode.CardEditor;
        _cardEditorWindow.SetCurrentCard(name);
    }

    public Dictionary<EditorContentMode, string> ButtonModeTable = new Dictionary<EditorContentMode, string>() {
        { EditorContentMode.EnemyEditor, "General Editor" },
        { EditorContentMode.CardEditor, "Card Editor" },
        { EditorContentMode.FusionEditor, "Fusion Editor" },
        { EditorContentMode.Patches, "Patches" },
        { EditorContentMode.StringEditor, "String Editor" },
        { EditorContentMode.ImageEditor, "Image Editor" },
        { EditorContentMode.MusicEditor, "Music Editor" },
        { EditorContentMode.Randomiser, "Randomiser" },
    };


    public void Render()
    {
        rlImGui.Begin();
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10f, 10f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10f, 10f));
        ImGui.Begin("Dotr Modding tool 2",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBringToFrontOnFocus);
        ImGui.SetWindowPos(Vector2.Zero);
        ImGui.SetWindowSize(new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight()));
        //Console.WriteLine(ImGui.GetWindowSize());
        CheckForHotkeys();
        ImGui.PopStyleVar(2);
        buttonWidthScaled = Math.Max(200f * AspectRatio.X, 100);
        buttonHeightRatio = Math.Max(100f * AspectRatio.Y, 50);
        buttonSpacingScaled = Math.Max(10f * AspectRatio.X, 5);

        if (disabledTimer > maxDisabledTime)
        {
            Disabled = false;
            disabledTimer = 0;
        }
        if (Disabled)
        {
            ImGui.BeginDisabled();
            disabledTimer += ImGui.GetIO().DeltaTime;
        }
        // Locale Management : Force '.', also get rid of float-irrelevant digits
        ImGui.Text($"FPS: {(ImGui.GetIO().Framerate).ToString("0.000").Replace(',','.')}");
        if (DataAccess.Instance.IsIsoLoaded)
        {
            ImGui.PushFont(FontManager.GetBestFitFont("Vanilla Iso"));
            string hash = DataAccess.Instance.CurrentHash;
            
            /* See "Reverting fix for Vanilla Size check, as MD5 is now used in Snapshots."
             * We basically want top priority on "Calculating Hash" now, so we know when to Snapshot.
             * IsVanillaSize remains a legit check. */
            if (hash == String.Empty)
            {
                ImGui.Text("Calculating Hash");
            }
            else if (!DataAccess.Instance.IsVanillaSize || (hash != String.Empty && hash != DataAccess.VanillaHash))
            {
                ImGui.TextColored(new GuiColour(Color.Crimson).value, "Modded ISO");
            }
            else
            {
                ImGui.TextColored(new GuiColour(Color.Green).value, "Vanilla ISO");
            }
            ImGui.PopFont();
        }


        ImGui.PushFont(menuBarFont);
        if (ImGui.BeginMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("Open ISO"))
                {
                    OpenIso();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Ctrl + O");
                    ImGui.EndTooltip();
                }

                ImGui.Spacing();

                ImGui.BeginDisabled(!dataAccess.IsIsoLoaded);

                if (ImGui.BeginMenu("Save"))
                {
                    if (ImGui.MenuItem("Save Data"))
                    {
                        if (dataAccess.IsIsoLoaded)
                        {
                            SaveChanges(false);
                        }
                        else
                        {
                            _modalPopup.Show("No ISO open to save to");
                        }
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text("Saves all non-String data");
                        ImGui.Text("Ctrl + S");
                        ImGui.EndTooltip();
                    }

                    ImGui.Spacing();

                    if (ImGui.MenuItem("Save Strings"))
                    {
                        if (dataAccess.IsIsoLoaded)
                        {
                            SaveStringAsync();
                        }
                        else
                        {
                            _modalPopup.Show("No ISO open to save to");
                        }
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text("Saves String changes");
                        ImGui.EndTooltip();
                    }
                    
                    ImGui.Spacing();

                    if (ImGui.MenuItem("Save ALL"))
                    {
                        if (dataAccess.IsIsoLoaded)
                        {
                            SaveChanges(true);
                        }
                        else
                        {
                            _modalPopup.Show("No ISO open to save to");
                        }
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text("Saves data including Strings");
                        ImGui.Text("Ctrl + Alt + S");
                        ImGui.EndTooltip();
                    }

                    ImGui.EndMenu();
                }

                if (ImGui.MenuItem("Print Passwords"))
                {
                    if (dataAccess.IsIsoLoaded)
                    {
                        PrintPasswords();
                    }
                    else
                    {
                        _modalPopup.Show("No ISO open to print Passwords from");
                    }
                }

                ImGui.Spacing();

                if (ImGui.BeginMenu("Changelog"))
                {
                    /* Moving Auto Changelog to Options.
                     * It turns out it does have an effect when Loading an ISO,
                     * but it cannot toggle on/off before actually Loading an ISO.
                     * (It was totally invisible before ; it remains disabled here, including Tooltip.) */

                    if (ImGui.MenuItem("Take Old Snapshot"))
                    {
                        // Now required. May happen if rushing Open ISO into Take Snapshot within seconds.
                        // Pretty rare outside of Vanilla though, and cashed back later on anyway. See "Generate Changelog".
                        if (DataAccess.Instance.CurrentHash == String.Empty)
                        {
                            _modalPopup.Show("Calculating MD5 hash data.\nPlease wait for calculation to finish, then try again.");
                        }
                        else
                        {
                            ChangelogManager.OldSnapshot = new ModSnapshot();
                        }
                    }

                    if (ImGui.MenuItem("Take New Snapshot"))
                    {
                        // Rare, but required. See "Old Snapshot".
                        if (DataAccess.Instance.CurrentHash == String.Empty)
                        {
                            _modalPopup.Show("Calculating MD5 hash data.\nPlease wait for calculation to finish, then try again.");
                        }
                        else
                        {
                            ChangelogManager.NewSnapshot = new ModSnapshot();
                        }
                    }

                    if (ImGui.MenuItem("Generate Changelog"))
                    {
                        if (ChangelogManager.OldSnapshot is null)
                        {
                            _modalPopup.Show("Old Snapshot is missing");
                        }
                        /* There is a flaw in that Snapshots are persistent during a session ;
                         * asking for a Changelog with a null NewSnapshot is a bad pattern,
                         * and it will give unexpected results when opening multiple ISOs.
                         * That said, if you really know what you're doing, it's a non-issue,
                         * as actual file name bugs are now fixed, so it can't overwrite another ISO's log.
                         * Leaving the check below like this so it's easy to comment out - at your own risk. */
                        else if (ChangelogManager.NewSnapshot is null)
                        {
                            _modalPopup.Show("New Snapshot is missing");
                        }
                        else
                        {
                            // Fallback behavior ; check warning above
                            if (ChangelogManager.NewSnapshot is null)
                            {
                                ChangelogManager.NewSnapshot = new ModSnapshot();
                            }
                            
                            // Superfast Changelog time, shrunk beyond 90%.
                            // The older code wasn't truly async - but we honestly no longer even need going async
                            string changelogText =
                                ChangelogManager.FormatResultsAsString(ChangelogManager.CompareAll());
                            Console.WriteLine("Passed Comparison");
                            File.WriteAllText($"Logs/Changelog_{ChangelogManager.NewSnapshot.SnapshotHash}-{ChangelogManager.OldSnapshot.SnapshotHash}.txt",
                                changelogText);
                            _modalPopup.Show("Changelog saved in Logs :\n" +
                                             ChangelogManager.NewSnapshot.SnapshotHash + "\nfrom " +
                                             ChangelogManager.OldSnapshot.SnapshotHash, "Changelog");
                        }
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text("Compare Old and New Snapshots");
                        ImGui.EndTooltip();
                    }

                    ImGui.EndMenu();
                }

                ImGui.Spacing();

                if (ImGui.BeginMenu("Import"))
                {
                    if (ImGui.MenuItem("Import Strings"))
                    {
                        DialogResult result = Dialog.FileOpen("json");
                        if (result.IsOk)
                        {
                            if (StringEditor.ImportStringsToJSON(result.Path))
                            {
                                _modalPopup.Show("Successfully imported json", "String Import");

                            }
                            else
                            {
                                _modalPopup.Show("Failed to import json file");
                            }
                        }
                    }

                    if (ImGui.MenuItem("Import Card Data"))
                    {
                        DialogResult result = Dialog.FileOpen("csv");
                        if (result.IsOk)
                        {
                            CardEditorWindow.ImportMonstersFromCSV(result.Path);
                        }
                        else
                        {
                            _modalPopup.Show("Failed to card data");
                        }
                    }

                    if (ImGui.MenuItem("Import Fusion Data"))
                    {
                        DialogResult result = Dialog.FileOpen("csv");
                        if (result.IsOk)
                        {
                            string isoPath = result.Path;
                            if (result.Path.EndsWith(".csv"))
                            {
                                FusionData.ImportFromCSV(isoPath);
                                _fusionEditorWindow.AllFusionData = FusionData.FusionTableData.ToList();
                            }
                            else
                            {
                                _modalPopup.Show("Failed to import fusion csv");
                            }
                        }
                    }

                    if (ImGui.MenuItem("Import Maps"))
                    {
                        var result = Dialog.FileOpen("txt");
                        if (result.IsOk)
                        {
                            Map.ImportMapFile(result.Path);
                        }
                        else
                        {
                            _modalPopup.Show("Failed to import map txt file");
                        }
                    }

                    if (ImGui.MenuItem("Import Randomiser Settings"))
                    {
                        var result = Dialog.FileOpen("json");
                        if (result.IsOk)
                        {
                            _randomiserWindow.ImportFromJson(result.Path);
                        }
                        else
                        {
                            _modalPopup.Show("Failed to import randomiser settings");
                        }
                    }

                    if (ImGui.MenuItem("Import Patches"))
                    {
                        var result = Dialog.FileOpen("json");
                        if (result.IsOk)
                        {
                            _gameplayPatchesWindow.ImportFromJson(result.Path);
                        }
                        else
                        {
                            _modalPopup.Show("Failed to import patches");
                        }
                    }

                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Export"))
                {
                    if (ImGui.MenuItem("Export Strings"))
                    {
                        DialogResult result = Dialog.FileSave("json");
                        if (result.IsOk)
                        {
                            StringEditor.ExportStringsToJSON($"{result.Path}.json");
                        }
                        else
                        {
                            _modalPopup.Show("Failed to export strings");
                        }
                    }

                    if (ImGui.MenuItem("Export Card Data"))
                    {
                        DialogResult result = Dialog.FileSave("csv");
                        if (result.IsOk)
                        {
                            string exportPath = result.Path;
                            CardEditorWindow.ExportMonstersToCSV($"{exportPath}.csv");
                        }
                        else
                        {
                            _modalPopup.Show("Failed to export card data ");
                        }
                    }

                    if (ImGui.MenuItem("Export Fusion Data"))
                    {
                        DialogResult result = Dialog.FileSave("csv");
                        if (result.IsOk)
                        {
                            string isoPath = result.Path;
                            FusionData.ExportToCSV($"{isoPath}.csv");
                        }
                        else
                        {
                            _modalPopup.Show("Failed to export fusion csv");
                        }
                    }

                    if (ImGui.MenuItem("Export Maps"))
                    {
                        var result = Dialog.FileSave("txt");
                        if (result.IsOk)
                        {
                            Map.ExportMapsToFile($"{result.Path}.txt");
                        }
                        else
                        {
                            _modalPopup.Show("Failed to export map text file");
                        }
                    }

                    if (ImGui.MenuItem("Export Randomiser Settings"))
                    {
                        var result = Dialog.FileSave("json");
                        if (result.IsOk)
                        {
                            _randomiserWindow.ExportToJson($"{result.Path}.json");
                        }
                        else
                        {
                            _modalPopup.Show("Failed to export randomiser settings");
                        }
                    }

                    if (ImGui.MenuItem("Export Patches"))
                    {
                        var result = Dialog.FileSave("json");
                        if (result.IsOk)
                        {
                            _gameplayPatchesWindow.ExportToJson($"{result.Path}.json");
                        }
                        else
                        {
                            _modalPopup.Show("Failed to export patches");
                        }
                    }

                    ImGui.EndMenu();
                }

                ImGui.EndDisabled();

                ImGui.EndMenu();
            }
            
            ImGui.Spacing();

            if (ImGui.BeginMenu("Options"))
            {
                if (ImGui.MenuItem("Toggle Image tooltips", null, ref UserSettings.ToggleImageTooltips))
                {
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Show Images when hovering over items");
                    ImGui.Text("Ctrl + T");
                    ImGui.EndTooltip();
                }

                ImGui.Spacing();

                if (ImGui.MenuItem("Performance Mode", null, ref UserSettings.performanceMode)) 
                {
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Caps the framerate to 30fps when the window is not focused\nand no item is hovered");
                    ImGui.Text("Ctrl + P");
                    ImGui.EndTooltip();
                }

                ImGui.Spacing();
                
                if (ImGui.MenuItem("Use original Strings", null, ref UserSettings.UseDefaultNames))
                {
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Shows Vanilla texts in editors rather than current ones\n" +
                               "for cards and characters");
                    ImGui.Text("Ctrl + D");
                    ImGui.EndTooltip();
                }
                
                ImGui.Spacing();
                
                if (ImGui.MenuItem("Use original Images", null, ref UserSettings.UseOriginalImages))
                {
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Shows Vanilla images in editors rather than current ones");
                    ImGui.Text("Ctrl + I");
                    ImGui.EndTooltip();
                }

                ImGui.Spacing();
                
                if (ImGui.MenuItem("Auto Changelog", null, ref UserSettings.AutoChangelog))
                {
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Create Changelog files when Saving modifications");
                    ImGui.Text("Automatically assigns Old Snapshots when Loading ISOs");
                    ImGui.Text("Automatically assigns New Snapshots when Saving ISOs");
                    ImGui.EndTooltip();
                }
                
                ImGui.EndMenu();
            }
            
            ImGui.Spacing();
            
            if (ImGui.MenuItem("Credits"))
            {
                isCreditsOpen = true;
            }
            if (isCreditsOpen)
            {
                ImGui.OpenPopup("Credits");
            }
            if (ImGui.BeginPopupModal("Credits", ref isCreditsOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.PushFont(mainFont);
                TextRenderer.Text("Credits:", TextEffect.Breathing);

                if (TextRenderer.Selectable("Batzpup: I made this", false, batzpupCreditEffect, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    OpenUrl("https://github.com/batzpup");
                }

                if (ImGui.Selectable("Blayr: Original modding tool developer", false,
                        ImGuiSelectableFlags.AllowDoubleClick))
                {
                    OpenUrl("https://github.com/Blayr");
                }
                if (ImGui.Selectable("This tool is heavily based on Blayr's original tool", false,
                        ImGuiSelectableFlags.AllowDoubleClick))
                {
                    OpenUrl("https://github.com/Blayr/DOTR-Modding-Tool");
                }

                if (ImGui.Selectable("GenericMadScientist: Rom Mapping/Documentation, Reverse Engineering, Image and Text Extraction", false,
                        ImGuiSelectableFlags.AllowDoubleClick))
                {
                    OpenUrl("https://github.com/GenericMadScientist");
                }

                if (ImGui.Selectable("Clovis: Redux, All Cards, and Redux 2 Mod Author", false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    OpenUrl("https://www.youtube.com/@ClovissenpaiDotR");
                }

                if (ImGui.Selectable("hippochan: Original Map Editor and Starter Deck Image Fixes", false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    OpenUrl("https://github.com/rjoken");
                }

                /* Uncomment when SP Wave 3 (Fusion Engine) is up
                if (ImGui.Selectable("StarlightPhoenix: SPEX Mod Author ; Fusion Engine and some Patches", false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    OpenUrl("https://www.youtube.com/@StarlightPhoenixProject");
                }
                */

                if (ImGui.Selectable("LordMewTwo73 for the text editing capability", false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    OpenUrl("https://github.com/LordMewtwo73/YGO-DOTR-Text-Editor");
                }
                
                ImGui.Separator();
                ImGui.TextColored(new GuiColour(Color.Cyan).value, "Code Contributors:");

                if (ImGui.Selectable("hippochan", false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    OpenUrl("https://github.com/rjoken");
                }

                if (ImGui.Selectable("StarlightPhoenix", false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    OpenUrl("https://codeberg.org/StarlightPhoenixProject");
                }
                
                ImGui.Text("MonoCh");

                ImGui.Text("Blayr");

                ImGui.Separator();
                ImGui.TextColored(new GuiColour(Color.MediumSpringGreen).value, "Testers:");
                
                TextRenderer.Text("StarlightPhoenix", sppCreditEffect);

                TextRenderer.Text("Woomy", woomyEffect);

                ImGui.Spacing();
                
                if (ImGui.Button("Close"))
                {
                    isCreditsOpen = false;
                }
#if DEBUG
                ImGui.SameLine();
                if (ImGui.Button("Text Effects Demo"))
                {
                    // modal blocks other windows, so close it before opening the demo
                    isCreditsOpen = false;
                    ImGui.CloseCurrentPopup();
                    _textEffectDemoWindow.IsOpen = true;
                }
#endif

                ImGui.EndPopup();
                ImGui.PopFont();
            }
            ImGui.Spacing();
            if (ImGui.MenuItem("Check for Updates"))
            {
                Task.Run(async () =>
                {
                    Disabled = true;
                    await Updater.CheckForUpdates(false);
                    Disabled = false;
                });
            }
            ImGui.Spacing();
            ImGui.EndMenuBar();
            ImGui.PopFont();

        }
        if (isSavingStrings)
        {
            _modalPopup.Show("Compressing strings", "Saving strings", null, ImGuiModalPopup.ShowType.NoButton);
        }

        DrawLeftPanel();
        ImGui.SameLine();
        ImGui.BeginChild("MainContent", new Vector2(0, 0), ImGuiChildFlags.None);

        if (reloadStrings)
        {
            StringEditor.ReloadFromISO();
            reloadStrings = false;
        }


        RenderMainContent();


        ImGui.EndChild();
        ImGui.Columns(0);
        if (Disabled)
        {
            ImGui.EndDisabled();
        }
        else
        {
            disabledTimer = 0;
        }
        _modalPopup.Draw(mainFont);


        ImGui.End();
#if DEBUG
        // separate top-level window, so it's drawn outside the main window's Begin/End
        _textEffectDemoWindow.Render();
#endif
        rlImGui.End();
    }

    void PrintPasswords()
    {
        StringBuilder buffer = new StringBuilder();
        foreach (var card in CardConstant.List)
        {
            buffer.AppendLine($"{card.Name.Current} : {card.Password}");
        }
        File.WriteAllText("passwords.txt", buffer.ToString());
    }


    void SaveStringAsync(Action onComplete = null, bool showMessage = true)
    {
        if (!isSavingStrings)
        {
            // Set before starting the task so a second save on the next frame can't start a concurrent compression
            isSavingStrings = true;
            Disabled = true;
            Task.Run(() =>
            {
                bool saved = false;
                try
                {
                    saved = SaveStrings(showMessage);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"String save failed: {ex}");
                    _modalPopup.Show($"{ex.GetType().Name}: {ex.Message}", "String save failed");
                }
                finally
                {
                    if (saved)
                    {
                        reloadStrings = true;
                    }
                    else
                    {
                        // Keep the unsaved edits, just discard the encoder's pointer state from the failed attempt
                        StringEditor.StringEncoder = new StringEncoder();
                    }
                    isSavingStrings = false;
                    Disabled = false;
                }
                if (saved)
                {
                    onComplete?.Invoke();
                }
            });
        }

    }

    void CheckForHotkeys()
    {
        if (ImGui.Shortcut(ImGuiKey.ModCtrl | ImGuiKey.S, ImGuiInputFlags.RouteGlobal))
        {
            SaveChanges(false);
        }
        else if (ImGui.Shortcut(ImGuiKey.ModCtrl | ImGuiKey.S | ImGuiKey.ModAlt, ImGuiInputFlags.RouteGlobal))
        {
            SaveChanges(true);
        }
        else if (ImGui.Shortcut(ImGuiKey.ModCtrl | ImGuiKey.O, ImGuiInputFlags.RouteGlobal))
        {
            OpenIso();
        }
        else if (ImGui.Shortcut(ImGuiKey.ModCtrl | ImGuiKey.T, ImGuiInputFlags.RouteGlobal))
        {
            UserSettings.ToggleImageTooltips = !UserSettings.ToggleImageTooltips;
        }
        else if (ImGui.Shortcut(ImGuiKey.ModCtrl | ImGuiKey.P, ImGuiInputFlags.RouteGlobal))
        {
            UserSettings.performanceMode = !UserSettings.performanceMode;
        }
        else if (ImGui.Shortcut(ImGuiKey.ModCtrl | ImGuiKey.D, ImGuiInputFlags.RouteGlobal))
        {
            UserSettings.UseDefaultNames = !UserSettings.UseDefaultNames;
        }
        else if (ImGui.Shortcut(ImGuiKey.ModCtrl | ImGuiKey.I, ImGuiInputFlags.RouteGlobal))
        {
            UserSettings.UseOriginalImages = !UserSettings.UseOriginalImages;
        }
        else if (ImGui.Shortcut(ImGuiKey.ModCtrl | ImGuiKey.U, ImGuiInputFlags.RouteGlobal))
        {
            Task.Run(async () =>
            {
                Disabled = true;
                await Updater.CheckForUpdates(false);
                Disabled = false;

            });
        }
        else if (ImGui.Shortcut(ImGuiKey.ModCtrl | ImGuiKey.C, ImGuiInputFlags.RouteGlobal))
        {
            isCreditsOpen = true;
        }

    }

    void DrawLeftPanel()
    {
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new GuiColour(0, 189, 0).value);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new GuiColour(0, 189, 0).value);
        //Draw stuff

        ImGui.BeginChild("LeftSidePanel", new Vector2(buttonWidthScaled + (buttonSpacingScaled * 2), 0),
            ImGuiChildFlags.Border | ImGuiChildFlags.NavFlattened | ImGuiChildFlags.AlwaysUseWindowPadding);
        ImGui.PushFont(FontManager.GetBestFitFont("fusion editor", false, FontManager.FontFamily.NotoSansJP));
        foreach (var modeButtonPair in ButtonModeTable)
        {
            if (currentMode == modeButtonPair.Key)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new GuiColour(0, 189, 0).value);
            }
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (modeButtonPair.Key == EditorContentMode.MusicEditor)
                {
                    ImGui.PopStyleColor();
                    continue;
                }
            }
            if (ImGui.Button(modeButtonPair.Value, new Vector2(buttonWidthScaled, buttonHeightRatio)))
            {
                if (currentMode == EditorContentMode.MusicEditor)
                {
                    _musicEditorWindow.StopMusic();
                }
                currentMode = modeButtonPair.Key;
            }
            if (currentMode == modeButtonPair.Key)
            {
                ImGui.PopStyleColor();
            }
            ImGui.Dummy(new Vector2(0, buttonSpacingScaled));
        }
        ImGui.PopStyleColor(2);
        ImGui.PopFont();
        ImGui.EndChild();
    }

    void RenderMainContent()
    {
        ImageHelper.DefaultImageSize = new Vector2(ImGui.GetWindowSize().X / 18f, ImGui.GetWindowSize().X / 18f);
        switch (currentMode)
        {
            case EditorContentMode.EnemyEditor:
                _generalEditorWindow.Render();
                break;
            case EditorContentMode.CardEditor:
                _cardEditorWindow.Render();
                break;
            case EditorContentMode.FusionEditor:
                _fusionEditorWindow.Render();
                break;
            case EditorContentMode.Patches:
                _gameplayPatchesWindow.Render();
                break;
            case EditorContentMode.MusicEditor:
                _musicEditorWindow.Render();
                break;
            case EditorContentMode.StringEditor:
                _stringEditorWindow.Render();
                break;
            case EditorContentMode.ImageEditor:
                _imageEditorWindow.Render();
                break;
            case EditorContentMode.Randomiser:
                _randomiserWindow.Render();
                break;

        }
    }


    void OpenIso()
    {
        DialogResult result;
        if (string.IsNullOrEmpty(UserSettings.LastIsoPath))
        {
            result = Dialog.FileOpen("iso");
        }
        else
        {
            result = Dialog.FileOpen("iso", UserSettings.LastIsoPath);

        }
        if (result.IsOk)
        {
            string isoPath = result.Path;
            if (result.Path.EndsWith(".iso") || result.Path.EndsWith(".ISO"))
            {
                DataAccess.Instance.OpenIso(isoPath);
                DataAccess.Instance.CurrentHash = String.Empty;
                LoadDataFromIso();
                UserSettings.LastIsoPath = Path.GetDirectoryName(isoPath);
                
                /* Reverting fix for Vanilla Size check, as MD5 is now used in Snapshots,
                 * and Superfast Changelog otherwise would break on custom-sized mods.
                 * Since the latter actually already exist, they are trivially more likely
                 * than a potential illegitimate MD5 collision resulting from that. */
                //if (DataAccess.Instance.IsVanillaSize)
                if (true)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await DataAccess.Instance.GetMd5HashAsync();
                            Console.WriteLine("MD5 hash completed successfully.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"MD5 hash failed: {ex}");
                        }
                    });
                }

                if (UserSettings.AutoChangelog)
                {
                    ChangelogManager.OldSnapshot = new ModSnapshot();
                }
            }
            else
            {
                Console.WriteLine("Should show pop up error");
                _modalPopup.Show("Not an .iso file");
            }
        }
    }


    public void SaveChanges(bool saveStrings)
    {
        if (!dataAccess.IsIsoLoaded)
        {
            _modalPopup.Show("No ISO loaded to save");
            return;
        }

        _generalEditorWindow.DeckEditorWindow.SaveAllDecks();
        _generalEditorWindow.MapEditorWindow.SaveAllMaps();
        _cardEditorWindow.SaveCardChanges();
        _gameplayPatchesWindow.ApplyPatches();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _musicEditorWindow.SaveMusicChanges();
        }

        dataAccess.SaveCardDeckLeaderAbilities(CardDeckLeaderAbilities.Bytes);
        dataAccess.SaveMonsterEnchantData(MonsterEnchantData.Bytes);
        dataAccess.SaveDeckLeaderThresholds();
        dataAccess.SaveEnemyAiData(Enemies.AiBytes);
        dataAccess.SaveEnchantData();
        dataAccess.SaveDestinyDrawCards();

        Task.Run(async () => { await dataAccess.SaveImageData(); });

        dataAccess.SaveEffectData(Effects.MonsterEffectBytes, Effects.MagicEffectBytes);
        _fusionEditorWindow.SaveFusionChanges();
        UserSettings.SaveSettings();

        if (UserSettings.AutoChangelog)
        {
            // TODO : Auto Changelog can't catch String changes, since they're saved async.
            DataAccess.Instance.GetMd5Hash();
            ChangelogManager.NewSnapshot = new ModSnapshot();
            string changelogText = ChangelogManager.FormatResultsAsString(ChangelogManager.CompareAll());
            File.WriteAllText($"Logs/Changelog_{DataAccess.Instance.CurrentHash}.txt", changelogText);
        }
        else
        {
            // Fix MD5 calculation not updating when Saving
            Task.Run(async () => { await DataAccess.Instance.GetMd5HashAsync(); });
        }

        if (saveStrings)
        {
            SaveStringAsync(printComplete, false);
        }
        else
        {
            printComplete();
        }

    }

    void printComplete()
    {
        if (!_generalEditorWindow.DeckEditorWindow.modalPopup.showErrorPopup)
        {
            if (dataAccess.AllImagesLoaded)
            {
                _modalPopup.Show("Slow saving due to images", "Save successful");
            }
            else
            {
                _modalPopup.Show("Changes have been saved", "Save successful");
            }

        }
    }

    public bool SaveStrings(bool showMessage = true)
    {
        Card.RebuildStringCache();
        for (int i = 0; i < Card.cardNameList.Length; i++)
        {
            StringEditor.StringTable[i + StringEditor.CardNamesOffsetStart] = Card.cardNameList[i].Edited;
        }
        if (!StringEditor.HasChangesSinceLoad())
        {
            if (showMessage)
            {
                _modalPopup.Show("No string changes to save", "Save success");
            }
            return true;
        }
        stringCompressionProgress = 0;
        StringEditor.StringEncoder.CompressStrings(StringEditor.StringTable.Values.ToList(), ref stringCompressionProgress);
        StringEditor.StringEncoder.ExportToBytes();
        if (StringEditor.StringBytes.Count > DataAccess.EnglishTextLength)
        {
            _modalPopup.Show("Too many bytes to save. Reduce string count or length", "Save failed");
            return false;
        }
        dataAccess.SaveStringData();
        if (showMessage)
        {
            _modalPopup.Show("Strings saved successfully", "Save success");
        }

        return true;
    }


    void LoadDataFromIso()
    {
        StringEditor.Run();
        CardConstant.LoadFromBytes(dataAccess.LoadCardConstantData());
        dataAccess.LoadEnemyAIData();
        dataAccess.LoadDecksData();
        dataAccess.LoadDestinyDrawCards();
        _generalEditorWindow.MapEditorWindow.LoadMapData();
        _generalEditorWindow.MapEditorWindow.LoadTreasureCardData();
        _generalEditorWindow.DeckEditorWindow.UpdateDeckData();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _musicEditorWindow.LoadMusicFromIso();
        }

        dataAccess.LoadMonsterEquipCardData();
        dataAccess.LoadCardDeckLeaderAbilities();
        dataAccess.LoadEffectData();
        dataAccess.LoadLeaderThresholdData();
        dataAccess.LoadFusionData();
        dataAccess.LoadEnchantData();
        dataAccess.LoadImageData();
        Map.Initialise();
        OnIsoLoaded?.Invoke();
        StringEditor.ReloadStrings();

    }

    void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to open URL: " + ex.Message);
        }
    }

    public static void CreateCSVFromMonsterEffects(List<MonsterEffects> monsterEffectsList)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "AttackEffectName,AttackSearchMode,MovementEffectName,MovementSearchMode,NatureEffectName,NatureSearchMode,FlipEffectName,FlipSearchMode,DestructionEffectName,DestructionSearchMode");

        foreach (var monsterEffect in monsterEffectsList)
        {
            var row = new List<string>();

            foreach (var effect in monsterEffect.Effects)
            {
                string effectData = effect.effectName;
                string searchModeData = effect.SearchModeName;

                row.Add(effectData); // Effect Name column
                row.Add(searchModeData); // Search Mode Name column
            }
            sb.AppendLine(string.Join(",", row));
        }

        File.WriteAllText("MonsterEffects.csv", sb.ToString());
        Console.WriteLine("CSV file created successfully.");
    }
}