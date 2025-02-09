﻿using System;
using System.Collections.Generic;
using System.Linq;
using LobbyCompatibility.Enums;
using LobbyCompatibility.Models;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LobbyCompatibility.Features;

/// <summary>
///     Helper class for UI-related functions.
/// </summary>
internal static class UIHelper
{
    /// <summary>
    ///     Multiplies a <see cref="Transform" />'s sizeDelta as a <see cref="RectTransform" />. Returns false if transform is
    ///     invalid.
    /// </summary>
    /// <param name="transform"> The <see cref="Transform" /> to modify. </param>
    /// <param name="multiplier"> Amount to multiply the sizeDelta by. </param>
    public static bool TryMultiplySizeDelta(Transform? transform, Vector2 multiplier)
    {
        if (transform == null)
            return false;

        var rectTransform = transform.GetComponent<RectTransform>();
        if (rectTransform == null)
            return false;

        MultiplySizeDelta(rectTransform, multiplier);
        return true;
    }

    /// <summary>
    ///     Multiplies a <see cref="RectTransform" />'s sizeDelta.
    /// </summary>
    /// <param name="rectTransform"> The <see cref="RectTransform" /> to modify. </param>
    /// <param name="multiplier"> Amount to multiply the sizeDelta by. </param>
    private static void MultiplySizeDelta(RectTransform rectTransform, Vector2 multiplier)
    {
        var sizeDelta = rectTransform.sizeDelta;
        rectTransform.sizeDelta = new Vector2(sizeDelta.x * multiplier.x, sizeDelta.y * multiplier.y);
    }

    /// <summary>
    ///     Creates a new <see cref="TextMeshProUGUI" /> to be used as a template. Intended to be used as a modlist template.
    /// </summary>
    /// <param name="template"> The <see cref="TextMeshProUGUI" /> to base the template off of. </param>
    /// <param name="color"> Text color. </param>
    /// <param name="size"> The sizeDelta of the text's RectTransform. </param>
    /// <param name="maxFontSize"> The font's maximum size. </param>
    /// <param name="minFontSize"> The font's minimum size. </param>
    /// <param name="alignment"> How to align the text. </param>
    public static TextMeshProUGUI SetupTextAsTemplate(TextMeshProUGUI template, Color color, Vector2 size,
        float maxFontSize, float minFontSize, HorizontalAlignmentOptions alignment = HorizontalAlignmentOptions.Center)
    {
        var text = Object.Instantiate(template, template.transform.parent);

        // Set text alignment / formatting options
        text.rectTransform.anchoredPosition = new Vector2(0f, 0f);
        text.rectTransform.sizeDelta = size;
        text.horizontalAlignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMax = maxFontSize;
        text.fontSizeMin = minFontSize;
        text.enableWordWrapping = false;
        text.color = color;

        // Deactivate so we can use as a template later
        text.gameObject.SetActive(false);
        return text;
    }

    /// <summary>
    ///     Creates a new <see cref="TextMeshProUGUI" /> from a template. Intended to be used with the modlist.
    /// </summary>
    /// <param name="template"> The <see cref="TextMeshProUGUI" /> to use as a template. </param>
    /// <param name="content"> The text's content. </param>
    /// <param name="yPosition"> Sets the text's <see cref="RectTransform.anchoredPosition.y" />. </param>
    /// <param name="overrideColor"> A color to override the template color with. </param>
    public static TextMeshProUGUI CreateTextFromTemplate(TextMeshProUGUI template, string content, float yPosition,
        Color? overrideColor = null)
    {
        var text = Object.Instantiate(template, template.transform.parent);

        if (overrideColor != null)
            text.color = overrideColor!.Value;

        text.rectTransform.anchoredPosition = new Vector2(0f, yPosition);
        text.text = content;
        text.gameObject.SetActive(true);

        return text;
    }

    /// <summary>
    ///     Creates a list of <see cref="TextMeshProUGUI" /> from a <see cref="LobbyDiff" />'s plugins
    /// </summary>
    /// <param name="lobbyDiff"> The <see cref="LobbyDiff" /> to generate text from. </param>
    /// <param name="textTemplate"> The <see cref="TextMeshProUGUI" /> to use for the mod text. </param>
    /// <param name="headerTextTemplate"> The <see cref="TextMeshProUGUI" /> to use for the category header text. </param>
    /// <param name="textSpacing"> The amount of spacing around mod text. </param>
    /// <param name="headerSpacing"> The amount of padding around category header text. </param>
    /// <param name="startPadding"> The amount of padding to start with. </param>
    /// <param name="compactText"> Whether to remove versions from mod names. </param>
    /// <param name="maxLines"> The maximum amount of total text lines to generate. </param>
    /// <returns> The <see cref="TextMeshProUGUI" /> list, the total UI length, and the amount of plugins used in generation. </returns>
    public static (List<TextMeshProUGUI>, float, int) GenerateTextFromDiff(
        LobbyDiff lobbyDiff,
        TextMeshProUGUI textTemplate,
        TextMeshProUGUI headerTextTemplate,
        float textSpacing,
        float headerSpacing,
        float? startPadding = null,
        bool compactText = false,
        int? maxLines = null)
    {
        // TODO: Replace with pooling if we need the performance from rapid scrolling
        // TODO: Replace this return type with an actual type lol. Need to refactor this a bit

        List<TextMeshProUGUI> generatedText = new();
        var padding = startPadding ?? 0f;
        var lines = 0;
        var pluginLines = 0;

        foreach (var compatibilityResult in Enum.GetValues(typeof(PluginDiffResult)).Cast<PluginDiffResult>())
        {
            var plugins = lobbyDiff.PluginDiffs.Where(
                pluginDiff => pluginDiff.PluginDiffResult == compatibilityResult).ToList();

            if (plugins.Count == 0)
                continue;

            // adds a few units of extra header padding
            padding += headerSpacing - textSpacing;

            // end linecount sooner if we're about to create a header - no point in showing a blank header
            if (maxLines != null && lines > maxLines - 1)
                break;

            // Create the category header
            var headerText = CreateTextFromTemplate(headerTextTemplate,
                LobbyHelper.GetCompatibilityHeader(compatibilityResult) + ":", -padding);

            generatedText.Add(headerText);
            padding += headerSpacing;
            lines++;

            // Add each plugin
            foreach (var plugin in plugins)
            {
                if (lines > maxLines)
                    break;

                var modText = CreateTextFromTemplate(textTemplate, compactText ? plugin.GUID : plugin.GetDisplayText(),
                    -padding, plugin.GetTextColor());
                modText.richText = false; // Disable rich text to avoid people injecting weird text into the modlist
                generatedText.Add(modText);
                padding += textSpacing;
                lines++;
                pluginLines++;
            }
        }

        return (generatedText, padding, pluginLines);
    }
}