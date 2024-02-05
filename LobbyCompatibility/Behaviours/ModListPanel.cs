﻿using System.Collections.Generic;
using LobbyCompatibility.Features;
using LobbyCompatibility.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using Color = UnityEngine.Color;
using LobbyCompatibility.Enums;
using LobbyCompatibility.Pooling;
using System.Linq;
using System;
using static System.Net.Mime.MediaTypeNames;

namespace LobbyCompatibility.Behaviours;

/// <summary>
///     Mod list panel used to display a lobby's diff.
/// </summary>
public class ModListPanel : MonoBehaviour
{
    public static ModListPanel? Instance;

    private static readonly Vector2 NotificationWidth = new(1.6f, 1.75f);
    private readonly List<PluginDiffSlot?> _spawnedPluginDiffSlots = new();
    private readonly List<PluginCategorySlot?> _spawnedPluginCategorySlots = new();

    private RectTransform? _panelTransform;

    // Needed for scrolling / content size recalculation
    private ScrollRect? _scrollRect;
    private TextMeshProUGUI? _titleText;

    private PluginDiffSlotPool? _pluginDiffSlotPool;
    private PluginCategorySlotPool? _pluginCategorySlotPool;

    /// <summary>
    ///     Assign instance on awake so we can access it statically
    /// </summary>
    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    ///     Set up our mod list panel using notification & lobby list objects as donors
    /// </summary>
    /// <param name="panel"> A notification panel to use as a donor </param>
    /// <param name="scrollViewTemplate"> A lobby list's scroll view to use as a donor </param>
    public void SetupPanel(GameObject panel, Transform scrollViewTemplate)
    {
        var panelImage = panel.transform.Find("Panel")?.GetComponent<Image>();
        _panelTransform = panelImage?.rectTransform;
        if (panelImage == null || _panelTransform == null)
            return;

        // Get "dismiss" button so we can inject some custom behaviour
        var button = _panelTransform.Find("ResponseButton")?.GetComponent<Button>();
        var buttonTransform = button?.GetComponent<RectTransform>();
        if (button == null || buttonTransform == null)
            return;

        _titleText = _panelTransform.Find("NotificationText")?.GetComponent<TextMeshProUGUI>();
        if (_titleText == null)
            return;

        // Move header up
        _titleText.rectTransform.anchoredPosition = new Vector2(-2f, 155f);

        // Initialize scroll view by taking the game's lobby list and modifying it
        _scrollRect = SetupScrollRect(_panelTransform, scrollViewTemplate);
        if (_scrollRect == null)
            return;

        // Initialize modlist slots
        SetupModListSlots(_panelTransform, _scrollRect, _titleText.color);

        // Increase panel opacity to 100% so we can't see error messages underneath (if they exist)
        panelImage.color = new Color(panelImage.color.r, panelImage.color.g, panelImage.color.b, 1);

        // Multiply panel element sizes to make the hover notification skinnier
        UIHelper.TryMultiplySizeDelta(_panelTransform, NotificationWidth);
        UIHelper.TryMultiplySizeDelta(_panelTransform.Find("Image"), NotificationWidth);
        UIHelper.TryMultiplySizeDelta(_panelTransform.Find("NotificationText"), NotificationWidth);

        // Set button to be consistently spaced from the bottom of the panel 
        // This is the exact pixel distance the "Back" button is from the bottom on normal panels. TODO: do this dynamically based on notificationWidth
        buttonTransform.anchoredPosition = new Vector2(buttonTransform.anchoredPosition.x, -110.5f);

        // Inject custom button behaviour so it doesn't force you back to the main menu
        button.onClick.m_PersistentCalls.Clear();
        button.onClick.AddListener(() => { SetPanelActive(false); });

        SetPanelActive(false);
    }

    /// <summary>
    ///     Set up the scroll rect for the mod list panel using a lobby list's scroll rect as a donor
    /// </summary>
    /// <param name="panelTransform"> The mod list panel's transform </param>
    /// <param name="scrollViewTemplate"> A lobby list's scroll rect to use as a donor </param>
    /// <param name="defaultTextColor"> The default text color to use for the mod list panel </param>
    /// <returns> The <see cref="ScrollRect"/>. </returns>
    private ScrollRect? SetupScrollRect(RectTransform panelTransform, Transform scrollViewTemplate)
    {
        // Setup scrollRect for panel
        var scrollRectObject = Instantiate(scrollViewTemplate, panelTransform);
        var scrollRectTransform = scrollRectObject.GetComponent<RectTransform>();
        var scrollRect = scrollRectObject.GetComponent<ScrollRect>();
        if (scrollRectTransform == null || scrollRect == null)
            return null;

        // Delete duplicated lobby manager (not sure why it's on this object?)
        var lobbyManager = scrollRectObject.GetComponentInChildren<SteamLobbyManager>();
        if (lobbyManager != null)
            Destroy(lobbyManager);

        // Set pos/scale
        scrollRectTransform.anchoredPosition = new Vector2(15f, -30f);
        scrollRectTransform.sizeDelta = new Vector2(-30f, -100f);

        // Reset scroll to default position
        scrollRect.verticalNormalizedPosition = 1f;

        // Setup ContentSizeFilter and VerticalLayoutGroup so diff elements are automagically spaced
        var verticalLayoutGroup = scrollRect.content.gameObject.AddComponent<VerticalLayoutGroup>();
        verticalLayoutGroup.childControlHeight = false;
        verticalLayoutGroup.childForceExpandHeight = false;
        var contentSizeFilter = verticalLayoutGroup.gameObject.AddComponent<ContentSizeFitter>();
        contentSizeFilter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return scrollRect;
    }

    private void SetupModListSlots(RectTransform panelTransform, ScrollRect scrollRect, Color defaultTextColor)
    {
        var text = scrollRect.GetComponentInChildren<TextMeshProUGUI>();

        // Setup text as template
        text.gameObject.SetActive(false);

        // Setup PluginDiffSlot template panel
        var pluginDiffSlot = new GameObject("PluginDiffSlot");
        var pluginDiffSlotImage = pluginDiffSlot.AddComponent<Image>();
        var pluginDiffSlotTransform = UIHelper.ApplyParentSize(pluginDiffSlot, text.transform.parent);
        pluginDiffSlotTransform.anchoredPosition = Vector2.zero;
        pluginDiffSlotTransform.sizeDelta = new Vector2(1, 20f);
        pluginDiffSlotImage.color = Color.clear;
        pluginDiffSlot.SetActive(false);

        // Setup PluginCategorySlot template panel, identical except for the height
        var categoryEntry = Instantiate(pluginDiffSlot, pluginDiffSlotTransform.parent);
        categoryEntry.GetComponent<RectTransform>().sizeDelta = new Vector2(1, 25f);

        // Setup all text for PluginDiffSlot
        var pluginNameText = UIHelper.SetupTextAsTemplate(text, pluginDiffSlotTransform, defaultTextColor, new Vector2(220f, 30f), 18.35f, 2f,
            HorizontalAlignmentOptions.Left, new Vector2(-72f, 0f));
        var versionText = UIHelper.SetupTextAsTemplate(text, pluginDiffSlotTransform, defaultTextColor, new Vector2(70f, 30f), 18.35f, 2f,
            HorizontalAlignmentOptions.Center, new Vector2(70f, 0f));
        var serverVersionText = UIHelper.SetupTextAsTemplate(text, pluginDiffSlotTransform, defaultTextColor, new Vector2(70f, 30f), 18.35f, 2f,
            HorizontalAlignmentOptions.Center, new Vector2(140f, 0f));

        // Finish PluginDiffSlot setup
        var diffSlot = pluginDiffSlot.AddComponent<PluginDiffSlot>();
        diffSlot.SetupText(pluginNameText, versionText, serverVersionText);

        // Setup all text for PluginCategorySlot
        var categoryText = UIHelper.SetupTextAsTemplate(text, categoryEntry.transform, defaultTextColor, new Vector2(290f, 35f), 18.35f, 2f,
            HorizontalAlignmentOptions.Center, new Vector2(-72f, 0f));

        // Finish PluginCategorySlot setup
        var categorySlot = categoryEntry.AddComponent<PluginCategorySlot>();
        categorySlot.SetupText(categoryText);
        categorySlot.gameObject.SetActive(false);

        // Initialize pools
        var pluginPoolsObject = new GameObject("PluginSlotPools");
        pluginPoolsObject.transform.SetParent(panelTransform);
        _pluginDiffSlotPool = pluginPoolsObject.AddComponent<PluginDiffSlotPool>();
        _pluginDiffSlotPool.InitializeUsingTemplate(diffSlot, diffSlot.transform.parent);
        _pluginCategorySlotPool = pluginPoolsObject.AddComponent<PluginCategorySlotPool>();
        _pluginCategorySlotPool.InitializeUsingTemplate(categorySlot, categorySlot.transform.parent);
    }

    /// <summary>
    ///     Open the panel and display a lobby's mod list diff
    /// </summary>
    /// <param name="lobbyDiff"> The lobby diff to display </param>
    /// <param name="titleOverride"> Override the title text of the mod list panel </param>
    public void DisplayNotification(LobbyDiff lobbyDiff, string? titleOverride = null)
    {
        if (_scrollRect == null)
            return;

        // Set scroll to zero
        _scrollRect.verticalNormalizedPosition = 1f;
        SetPanelActive(true);
        // EventSystem.current.SetSelectedGameObject(this.menuNotification.GetComponentInChildren<Button>().gameObject);

        DisplayModList(lobbyDiff, titleOverride);
    }

    /// <summary>
    ///     Display a lobby's mod list diff through the mod list panel
    /// </summary>
    /// <param name="lobbyDiff"> The lobby diff to display </param>
    /// <param name="titleOverride"> Override the title text of the mod list panel </param>
    private void DisplayModList(LobbyDiff lobbyDiff, string? titleOverride = null)
    {
        if (_pluginDiffSlotPool == null || _pluginCategorySlotPool == null || _titleText == null)
            return;

        // Despawn old diffs
        ClearSpawnedDiffs();

        // Create categories w/ mods
        foreach (var compatibilityResult in Enum.GetValues(typeof(PluginDiffResult)).Cast<PluginDiffResult>())
        {
            var plugins = lobbyDiff.PluginDiffs.Where(
                pluginDiff => pluginDiff.PluginDiffResult == compatibilityResult).ToList();

            if (plugins.Count == 0)
                continue;

            var pluginCategorySlot = _pluginCategorySlotPool.Spawn(compatibilityResult);
            _spawnedPluginCategorySlots.Add(pluginCategorySlot);

            // Respawn mod diffs
            foreach (var mod in plugins)
            {
                var pluginDiffSlot = _pluginDiffSlotPool.Spawn(mod);
                if (pluginDiffSlot == null)
                    continue;

                _spawnedPluginDiffSlots.Add(pluginDiffSlot);
            }
        }

        _titleText.text = titleOverride ?? lobbyDiff.GetDisplayText();
    }

    private void ClearSpawnedDiffs()
    {
        if (_pluginDiffSlotPool == null || _pluginCategorySlotPool == null)
            return;

        foreach (var pluginDiffSlot in _spawnedPluginDiffSlots)
        {
            if (pluginDiffSlot == null)
                continue;
            _pluginDiffSlotPool.Release(pluginDiffSlot);
        }

        foreach (var pluginCategorySlot in _spawnedPluginCategorySlots)
        {
            if (pluginCategorySlot == null)
                continue;
            _pluginCategorySlotPool.Release(pluginCategorySlot);
        }

        _spawnedPluginDiffSlots.Clear();
        _spawnedPluginCategorySlots.Clear();
    }

    /// <summary>
    ///     Set the panel's active state
    /// </summary>
    /// <param name="active"> Whether or not the panel should be active </param>
    private void SetPanelActive(bool active)
    {
        if (_panelTransform == null)
            return;

        // Disable the parent because it also contains a background image used for blocking raycasts
        _panelTransform.parent.gameObject.SetActive(active);
    }
}