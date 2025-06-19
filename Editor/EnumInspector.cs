using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

public class EnumEditorWindow : EditorWindow
{
    private MonoScript selectedScript;
    private string scriptPath;
    private Dictionary<string, List<EnumEntry>> loadedEnumData = new Dictionary<string, List<EnumEntry>>();
    private Vector2 scrollPosition;

    private string enumToRemoveFromGui = null;
    private bool saveAllChanges = false;
    private string newEnumNameInput = "";

    private readonly HashSet<string> newlyAddedEnumNames = new HashSet<string>();
    private readonly HashSet<string> enumsToDeleteOnSave = new HashSet<string>();

    [Serializable]
    public class EnumEntry
    {
        public string name;
        public string intValue;
        public string comment;
    }

    [MenuItem("Tools/Enum Editor")]
    public static void OpenWindow()
    {
        GetWindow<EnumEditorWindow>("Enum Editor");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();

        MonoScript newScript = (MonoScript)EditorGUILayout.ObjectField("Script", selectedScript, typeof(MonoScript), false);

        if (newScript != selectedScript)
        {
            selectedScript = newScript;
            if (selectedScript != null)
            {
                scriptPath = AssetDatabase.GetAssetPath(selectedScript);
                LoadEnums();
            }
            else
            {
                loadedEnumData.Clear();
                newlyAddedEnumNames.Clear();
                enumsToDeleteOnSave.Clear();
            }
            ResetPendingActions();
        }

        if (selectedScript == null)
        {
            EditorGUILayout.HelpBox("Select a C# script to view and edit enums.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        var currentEnumsToShow = loadedEnumData.Where(pair => !enumsToDeleteOnSave.Contains(pair.Key)).ToList();

        if (!currentEnumsToShow.Any() && string.IsNullOrWhiteSpace(newEnumNameInput))
        {
            EditorGUILayout.HelpBox("No enums loaded. Add a new enum below.", MessageType.Info);
        }

        foreach (var enumPair in currentEnumsToShow)
        {
            string enumName = enumPair.Key;
            var entries = enumPair.Value;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("enum " + enumName, EditorStyles.boldLabel);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                enumToRemoveFromGui = enumName;
            }
            EditorGUILayout.EndHorizontal();

            int entryToRemove = -1;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                EditorGUILayout.BeginHorizontal();

                string currentEntryName = entry.name;
                string newEntryName = EditorGUILayout.TextField(currentEntryName);

                if (newEntryName != currentEntryName)
                {
                    if (IsValidIdentifier(newEntryName) && !entries.Any(e => e != entry && e.name == newEntryName))
                    {
                        entry.name = newEntryName;
                    }
                    else if (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout)
                    {
                        EditorGUILayout.HelpBox("Invalid or duplicate name.", MessageType.Warning);
                    }
                }

                entry.intValue = EditorGUILayout.TextField(entry.intValue, GUILayout.Width(80));

                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    entryToRemove = i;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (entryToRemove >= 0)
            {
                entries.RemoveAt(entryToRemove);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Add Entry"))
            {
                entries.Add(new EnumEntry { name = "NewValue", intValue = "", comment = "" });
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Add New Enum", EditorStyles.boldLabel);
        newEnumNameInput = EditorGUILayout.TextField("Enum Name", newEnumNameInput);
        if (GUILayout.Button("Create New Enum") && !string.IsNullOrWhiteSpace(newEnumNameInput))
        {
            if (IsValidIdentifier(newEnumNameInput) && !loadedEnumData.ContainsKey(newEnumNameInput))
            {
                AddNewEnumToInMemory(newEnumNameInput);
                newEnumNameInput = "";
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Invalid or duplicate enum name. Please use valid C# identifier characters.", "OK");
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
        if (GUILayout.Button("Save All Changes to Script"))
        {
            saveAllChanges = true;
        }
        EditorGUILayout.Space();


        if (saveAllChanges)
        {
            PerformSaveOperations();
            ResetPendingActions();
        }
        else if (!string.IsNullOrEmpty(enumToRemoveFromGui))
        {
            if (EditorUtility.DisplayDialog("Confirm Deletion", $"Are you sure you want to remove the enum '{enumToRemoveFromGui}' from the GUI (will be removed from file on save)?", "Yes", "No"))
            {
                loadedEnumData.Remove(enumToRemoveFromGui);
                enumsToDeleteOnSave.Add(enumToRemoveFromGui);
                newlyAddedEnumNames.Remove(enumToRemoveFromGui);

                Repaint();
            }
            ResetPendingActions();
        }
    }

    private void ResetPendingActions()
    {
        enumToRemoveFromGui = null;
        saveAllChanges = false;
    }

    private bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }

    private void AddNewEnumToInMemory(string newEnumName)
    {
        if (!loadedEnumData.ContainsKey(newEnumName))
        {
            loadedEnumData[newEnumName] = new List<EnumEntry> {
                new EnumEntry { name = "DefaultValue", intValue = "", comment = "" }
            };
            newlyAddedEnumNames.Add(newEnumName);
            enumsToDeleteOnSave.Remove(newEnumName);
            Repaint();
        }
    }

    private void PerformSaveOperations()
    {
        List<string> currentFileLines = File.ReadAllLines(scriptPath).ToList();
        bool changed = false;

        List<string> linesAfterDeletions = new List<string>(currentFileLines);
        List<string> successfullyDeleted = new List<string>();

        foreach (string enumToDelete in enumsToDeleteOnSave)
        {
            if (FindEnumBlockIndices(enumToDelete, linesAfterDeletions, out int declarationLineIndex, out int openingBraceIndex, out int closingBraceIndex))
            {
                linesAfterDeletions.RemoveRange(declarationLineIndex, closingBraceIndex - declarationLineIndex + 1);
                if (declarationLineIndex > 0 && declarationLineIndex - 1 < linesAfterDeletions.Count && string.IsNullOrWhiteSpace(linesAfterDeletions[declarationLineIndex - 1]))
                {
                    linesAfterDeletions.RemoveAt(declarationLineIndex - 1);
                }
                changed = true;
                successfullyDeleted.Add(enumToDelete);
                Debug.Log($"Enum '{enumToDelete}' removed from script content.");
            }
            else
            {
                Debug.LogWarning($"Enum '{enumToDelete}' not found or malformed in script for removal. It might have been already removed or never existed.");
            }
        }
        currentFileLines = linesAfterDeletions;
        enumsToDeleteOnSave.ExceptWith(successfullyDeleted);

        HashSet<string> enumsCurrentlyInFile = new HashSet<string>();
        foreach (var enumPair in loadedEnumData)
        {
            if (!newlyAddedEnumNames.Contains(enumPair.Key) && !enumsToDeleteOnSave.Contains(enumPair.Key))
            {
                if (FindEnumBlockIndices(enumPair.Key, currentFileLines, out _, out _, out _))
                {
                    enumsCurrentlyInFile.Add(enumPair.Key);
                }
            }
        }

        foreach (var enumPair in loadedEnumData)
        {
            string enumName = enumPair.Key;
            List<EnumEntry> entries = enumPair.Value;

            if (enumsCurrentlyInFile.Contains(enumName) && !enumsToDeleteOnSave.Contains(enumName))
            {
                currentFileLines = UpdateExistingEnumInLines(enumName, entries, currentFileLines);
                changed = true;
            }
        }

        foreach (string newEnumName in newlyAddedEnumNames)
        {
            if (!enumsToDeleteOnSave.Contains(newEnumName))
            {
                currentFileLines = AddNewEnumToLines(newEnumName, loadedEnumData[newEnumName], currentFileLines);
                changed = true;
            }
        }

        if (changed)
        {
            File.WriteAllLines(scriptPath, currentFileLines);
            AssetDatabase.Refresh();
            Debug.Log("All changes saved to script: " + scriptPath);
        }
        else
        {
            Debug.Log("No changes detected to save.");
        }

        LoadEnums();
    }

    /// <summary>
    /// Loads enums and their entries from the selected C# script file.
    /// This method now uses a more robust brace level tracking to correctly scope enums.
    /// </summary>
    private void LoadEnums()
    {
        loadedEnumData.Clear();
        newlyAddedEnumNames.Clear();
        enumsToDeleteOnSave.Clear();

        if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath)) return;

        string[] lines = File.ReadAllLines(scriptPath);
        string currentEnumName = null;
        List<EnumEntry> currentEntries = null;

        // This stack will track the brace level at which a scope (like an enum, class, or namespace) *began*.
        Stack<int> scopeBraceLevels = new Stack<int>();
        int currentLineBraceLevel = 0; // The brace level of the *current line* itself

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            if (trimmed.StartsWith("using ")) continue;

            // First, adjust currentLineBraceLevel based on braces in the current line
            foreach (char c in trimmed)
            {
                if (c == '{') currentLineBraceLevel++;
                else if (c == '}') currentLineBraceLevel--;
            }

            Match enumDeclMatch = Regex.Match(line, @"^\s*(public|private|internal)?\s*enum\s+(\w+)\s*(\{)?");

            // If a new enum declaration is found
            if (enumDeclMatch.Success)
            {
                currentEnumName = enumDeclMatch.Groups[2].Value;
                currentEntries = new List<EnumEntry>();
                loadedEnumData[currentEnumName] = currentEntries;
                scopeBraceLevels.Push(currentLineBraceLevel);
                continue;
            }

            // If we are currently tracking an enum (currentEnumName is set)
            if (currentEnumName != null && currentEntries != null)
            {
                if (scopeBraceLevels.Count > 0)
                {
                    int enumStartBraceLevel = scopeBraceLevels.Peek(); // The level *at which this enum's body opened*

                    // If the current line's brace level is the same as the enum's start brace level,
                    // it means we've hit the closing brace of the enum.
                    if (currentLineBraceLevel == enumStartBraceLevel && trimmed.Contains("}"))
                    {
                        scopeBraceLevels.Pop(); // Exit this enum's scope
                        currentEnumName = null; // No longer tracking this enum
                        currentEntries = null;
                    }
                    // If we are deeper than the enum's starting brace level, it's potentially an entry
                    else if (currentLineBraceLevel > enumStartBraceLevel &&
                             !string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("//"))
                    {
                        // Match enum entry: Name = Value, // Comment
                        Match entryMatch = Regex.Match(trimmed, @"^(\w+)\s*(=\s*[^,]+?)?\s*,?\s*(//.*)?$");

                        if (entryMatch.Success)
                        {
                            string name = entryMatch.Groups[1].Value.Trim();
                            string value = entryMatch.Groups[2].Success ? entryMatch.Groups[2].Value.TrimStart('=').Trim() : "";
                            string comment = entryMatch.Groups[3].Success ? entryMatch.Groups[3].Value.Trim() : "";

                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                currentEntries.Add(new EnumEntry { name = name, intValue = value, comment = comment });
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Helper to find the start/end indices of an enum block within a list of lines.
    /// Returns true if found, false otherwise.
    /// </summary>
    private bool FindEnumBlockIndices(string enumName, List<string> lines, out int declarationLineIndex, out int openingBraceIndex, out int closingBraceIndex)
    {
        declarationLineIndex = -1;
        openingBraceIndex = -1;
        closingBraceIndex = -1;

        // Find the enum declaration line first
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            Match enumDeclMatch = Regex.Match(line, $@"(^\s*)(public|private|internal)?\s*enum\s+{enumName}\b");
            if (enumDeclMatch.Success)
            {
                declarationLineIndex = i;
                break;
            }
        }

        if (declarationLineIndex == -1)
        {
            return false;
        }

        // Find the opening brace starting from the declaration line
        for (int i = declarationLineIndex; i < lines.Count; i++)
        {
            if (lines[i].Contains("{"))
            {
                openingBraceIndex = i;
                break;
            }
        }

        if (openingBraceIndex == -1)
        {
            return false;
        }

        // Find the matching closing brace starting from the opening brace line
        int braceLevel = 0;
        for (int i = openingBraceIndex; i < lines.Count; i++)
        {
            string line = lines[i];
            if (line.Contains("{")) braceLevel++;
            if (line.Contains("}")) braceLevel--;

            if (braceLevel == 0 && i >= openingBraceIndex)
            {
                closingBraceIndex = i;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Updates an existing enum within a given list of lines.
    /// Does NOT write to file directly, returns modified list.
    /// </summary>
    private List<string> UpdateExistingEnumInLines(string enumName, List<EnumEntry> entries, List<string> originalLines)
    {
        if (!FindEnumBlockIndices(enumName, originalLines, out int declarationLineIndex, out int openingBraceIndex, out int closingBraceIndex))
        {
            Debug.LogWarning($"Enum '{enumName}' not found or malformed in script for update. This should not happen if it was in 'enumsCurrentlyInFile'.");
            return originalLines;
        }

        string enumIndentation = Regex.Match(originalLines[declarationLineIndex], @"^(\s*)").Groups[1].Value;

        originalLines.RemoveRange(declarationLineIndex, closingBraceIndex - declarationLineIndex + 1);

        List<string> newEnumContent = new List<string>();
        newEnumContent.Add($"{enumIndentation}public enum {enumName}");
        newEnumContent.Add($"{enumIndentation}{{");
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.name)) continue;
            string valuePart = string.IsNullOrWhiteSpace(entry.intValue) ? "" : $" = {entry.intValue}";
            string commentPart = string.IsNullOrWhiteSpace(entry.comment) ? "" : $" //{entry.comment}";
            newEnumContent.Add($"{enumIndentation}    {entry.name}{valuePart},{commentPart}");
        }
        newEnumContent.Add($"{enumIndentation}}}");

        originalLines.InsertRange(declarationLineIndex, newEnumContent);

        return originalLines;
    }

    /// <summary>
    /// Adds a new enum to the end of a given list of lines.
    /// Does NOT write to file directly, returns modified list.
    /// </summary>
    private List<string> AddNewEnumToLines(string newEnumName, List<EnumEntry> entries, List<string> originalLines)
    {
        string targetIndentation = "";
        int currentFileBraceLevel = 0;

        for (int i = 0; i < originalLines.Count; i++)
        {
            string line = originalLines[i];
            string trimmedLine = line.Trim();

            if (trimmedLine.Contains("{")) currentFileBraceLevel++;
            if (trimmedLine.Contains("}")) currentFileBraceLevel--;

            if (currentFileBraceLevel == 1 && Regex.IsMatch(line, @"^\s*namespace\s+\w+\s*(\{)?"))
            {
                Match indentMatch = Regex.Match(line, @"^(\s*)");
                if (indentMatch.Success)
                {
                    targetIndentation = indentMatch.Groups[1].Value + "    ";
                }
            }
        }

        List<string> newEnumContent = new List<string>
        {
            "",
            $"{targetIndentation}public enum {newEnumName}",
            $"{targetIndentation}{{"
        };

        // Iterate over the provided 'entries' list to add all actual enum entries
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.name)) continue;
            string valuePart = string.IsNullOrWhiteSpace(entry.intValue) ? "" : $" = {entry.intValue}";
            string commentPart = string.IsNullOrWhiteSpace(entry.comment) ? "" : $" //{entry.comment}";
            newEnumContent.Add($"{targetIndentation}    {entry.name}{valuePart},{commentPart}");
        }

        newEnumContent.Add($"{targetIndentation}}}");

        originalLines.AddRange(newEnumContent);

        return originalLines;
    }
}