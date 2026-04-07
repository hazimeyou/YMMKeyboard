using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows;
using YMMKeyboardPlugin.Logging;
using YMMKeyboardPlugin.Mapping;
using YMMKeyboardPlugin.Models;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;

namespace YMMKeyboardPlugin.Actions;

public static class LoadYmmtCatalogAction
{
    private const string LogCategory = "LoadYmmtCatalog";
    private static readonly AsyncLocal<string?> currentOperationId = new();
    private static int shapeMembersLogged;

    private static readonly JsonSerializerOptions itemJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static void Execute(string switchName, string? parameter)
    {
        var opId = Guid.NewGuid().ToString("N")[..8];
        currentOperationId.Value = opId;
        var sw = Stopwatch.StartNew();
        LogInfo($"Execute start. switch={switchName}, parameter={parameter ?? "(null)"}");
        try
        {
            var rawPath = parameter?.Trim();
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                LogWarn("Parameter is empty.");
                MessageBox.Show(
                    "Set .ymmt file path in parameter.\nExample: C:\\Users\\<user>\\Downloads\\test.ymmt",
                    "LoadYmmtCatalog");
                return;
            }

            var path = NormalizePath(rawPath);
            LogInfo($"Normalized path: {path}");
            if (!YmmtCatalogLoader.TryLoad(path, out var snapshot, out var error) || snapshot is null)
            {
                LogError($"TryLoad failed. path={path}, error={error}");
                MessageBox.Show($"Load failed.\nPath: {path}\nError: {error}", "LoadYmmtCatalog");
                return;
            }

            string savedPath;
            try
            {
                savedPath = SaveToPluginFolder(path);
            }
            catch (Exception ex)
            {
                LogError($"SaveToPluginFolder failed. source={path}", ex);
                MessageBox.Show($"Loaded but save failed.\nPath: {path}\nError: {ex.Message}", "LoadYmmtCatalog");
                return;
            }

            var importResult = ImportToTimeline(snapshot);
            LogInfo($"Import finished. source={snapshot.SourcePath}, templates={snapshot.Templates.Count}, imported={importResult.Imported}, skipped={importResult.Skipped}, note={importResult.Note}");

            var message = BuildSummary(snapshot, importResult);
            message += $"\n\nSavedTo: {savedPath}";
            message += $"\nLogFile: {PluginLogger.CurrentLogFilePath}";
            message += $"\nOperationId: {opId}";
            MessageBox.Show(message, "LoadYmmtCatalog");
        }
        catch (Exception ex)
        {
            LogError("Execute unexpected error.", ex);
            MessageBox.Show($"Unexpected error: {ex.Message}", "LoadYmmtCatalog");
        }
        finally
        {
            sw.Stop();
            LogInfo($"Execute end. elapsedMs={sw.ElapsedMilliseconds}");
            currentOperationId.Value = null;
        }
    }

    private static string NormalizePath(string rawPath)
    {
        var path = rawPath.Trim().Trim('"');
        path = Environment.ExpandEnvironmentVariables(path);
        return Path.GetFullPath(path);
    }

    private static ImportResult ImportToTimeline(YmmtProjectSnapshot snapshot)
    {
        var timeline = KeyboardAction.TimelineInstance;
        if (timeline is null)
        {
            LogWarn("Timeline is not available.");
            return new ImportResult(0, 0, "Timeline is not available.");
        }

        var template = snapshot.Templates.FirstOrDefault();
        if (template is null)
        {
            LogWarn("No template found.");
            return new ImportResult(0, 0, "No template found in .ymmt.");
        }

        var imported = 0;
        var skipped = 0;
        var notes = new List<string>();
        var baseFrame = timeline.CurrentFrame;
        var sourceFps = template.Fps > 0 ? (double)template.Fps : 60.0;
        var targetFps = GetTimelineFps(timeline, sourceFps);
        var frameScale = targetFps / sourceFps;
        if (Math.Abs(frameScale - 1.0) > 0.0001)
            notes.Add($"FrameScale applied: {sourceFps:0.###}fps -> {targetFps:0.###}fps (x{frameScale:0.###})");

        for (var i = 0; i < template.Items.Count; i++)
        {
            var src = template.Items[i];
            var frame = baseFrame + ScaleFrame(src.Frame, frameScale);
            var length = ScaleFrame(src.Length, frameScale, minimum: 1);
            if (!TryCreateTimelineItem(src, frame, length, out var item, out var reason) || item is null)
            {
                skipped++;
                LogWarn($"Item skipped. index={i}, type={src.ItemType}, frame={frame}, length={length}, reason={reason}");
                if (!string.IsNullOrWhiteSpace(reason) && notes.Count < 5)
                    notes.Add(reason);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(reason) && notes.Count < 5)
                notes.Add(reason);
            if (!string.IsNullOrWhiteSpace(reason))
                LogInfo($"Item note. index={i}, type={src.ItemType}, frame={frame}, length={length}, note={reason}");
            var itemFrame = GetPropertyInt(item, "Frame");
            var layer = GetPropertyInt(item, "Layer");
            if (Application.Current is not null)
                Application.Current.Dispatcher.Invoke(() => timeline.TryAddItems(new IItem[] { item }, itemFrame, layer));
            else
                timeline.TryAddItems(new IItem[] { item }, itemFrame, layer);
            imported++;
            LogInfo($"Item imported. index={i}, type={src.ItemType}, frame={itemFrame}, layer={layer}");
        }

        var noteText = notes.Count == 0 ? string.Empty : string.Join("\n", notes);
        return new ImportResult(imported, skipped, noteText);
    }

    private static bool TryCreateTimelineItem(YmmtItemSnapshot src, int frame, int length, out IItem? item, out string reason)
    {
        item = null;
        reason = string.Empty;

        try
        {
            if (string.Equals(src.ItemType, "AudioItem", StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(src.FilePath) || !File.Exists(src.FilePath))
                {
                    reason = $"Skip AudioItem: file not found ({src.FileName ?? "-"})";
                    return false;
                }

                var audio = new AudioItem(src.FilePath);
                SetIfWritable(audio, "Frame", frame);
                SetIfWritable(audio, "Layer", Math.Max(0, src.Layer));
                SetIfWritable(audio, "Length", length);
                item = audio;
                return true;
            }

            var itemsAssembly = typeof(AudioItem).Assembly;
            var type = itemsAssembly.GetType($"YukkuriMovieMaker.Project.Items.{src.ItemType}");
            if (type is null)
            {
                reason = $"Skip {src.ItemType}: unknown type";
                return false;
            }

            var deserializedItem = TryDeserializeTimelineItem(type, src.RawJson);
            var instance = deserializedItem ?? Activator.CreateInstance(type);
            if (instance is not IItem timelineItem)
            {
                reason = $"Skip {src.ItemType}: cannot create item";
                return false;
            }
            if (deserializedItem is null)
            {
                if (!TryApplyRawProperties(timelineItem, src.RawJson, out var appliedPropertyCount, out var applyDetail))
                    reason = $"Warn {src.ItemType}: internal parameters may not be fully restored";
                else
                    reason = $"Recovered {src.ItemType}: applied {appliedPropertyCount} properties from raw JSON ({applyDetail})";
            }

            SetIfWritable(timelineItem, "Frame", frame);
            SetIfWritable(timelineItem, "Layer", Math.Max(0, src.Layer));
            SetIfWritable(timelineItem, "Length", length);
            if (string.Equals(src.ItemType, "ShapeItem", StringComparison.Ordinal))
            {
                var shapeType = GetPropertyString(timelineItem, "ShapeType") ?? GetPropertyString(timelineItem, "ShapeType2") ?? "(null)";
                var shapeParamType = GetPropertyTypeName(timelineItem, "ShapeParameter");
                LogInfo($"ShapeItem resolved. shapeType={shapeType}, shapeParameterType={shapeParamType}");
            }
            item = timelineItem;
            return true;
        }
        catch (Exception ex)
        {
            reason = $"Skip {src.ItemType}: {ex.Message}";
            return false;
        }
    }

    private static double GetTimelineFps(Timeline timeline, double fallbackFps)
    {
        try
        {
            var videoInfoProperty = timeline.GetType().GetProperty("VideoInfo", BindingFlags.Public | BindingFlags.Instance);
            var videoInfo = videoInfoProperty?.GetValue(timeline);
            if (videoInfo is not null)
            {
                var fpsFromVideoInfo = GetPropertyDouble(videoInfo, "FPS");
                if (fpsFromVideoInfo > 0)
                    return fpsFromVideoInfo;
            }

            var value = GetPropertyDouble(timeline, "FPS");
            if (value > 0)
                return value;

            value = GetPropertyDouble(timeline, "FrameRate");
            if (value > 0)
                return value;
        }
        catch
        {
            // fallback
        }

        return fallbackFps;
    }

    private static int ScaleFrame(int frame, double scale, int minimum = 0)
    {
        var scaled = (int)Math.Round(Math.Max(0, frame) * scale, MidpointRounding.AwayFromZero);
        return scaled < minimum ? minimum : scaled;
    }

    private static int GetPropertyInt(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null)
            return 0;

        var value = property.GetValue(instance);
        return value switch
        {
            int i => i,
            long l => l > int.MaxValue ? int.MaxValue : l < int.MinValue ? int.MinValue : (int)l,
            _ => 0,
        };
    }

    private static double GetPropertyDouble(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null)
            return 0;

        var value = property.GetValue(instance);
        return value switch
        {
            double d => d,
            float f => f,
            decimal m => (double)m,
            int i => i,
            long l => l,
            string s when double.TryParse(s, out var parsed) => parsed,
            _ => 0,
        };
    }

    private static string? GetPropertyString(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null || !property.CanRead)
            return null;
        try
        {
            var value = property.GetValue(instance);
            return value?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string GetPropertyTypeName(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null || !property.CanRead)
            return "(missing)";
        try
        {
            var value = property.GetValue(instance);
            return value?.GetType().FullName ?? "(null)";
        }
        catch
        {
            return "(unreadable)";
        }
    }

    private static void SetIfWritable(object instance, string propertyName, object value)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property?.CanWrite != true)
            return;

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var converted = Convert.ChangeType(value, targetType);
        property.SetValue(instance, converted);
    }

    private static IItem? TryDeserializeTimelineItem(Type itemType, string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            LogWarn("TryDeserializeTimelineItem: rawJson empty.");
            return null;
        }

        try
        {
            var deserialized = JsonSerializer.Deserialize(rawJson, itemType, itemJsonSerializerOptions);
            if (deserialized is IItem item)
                return item;
        }
        catch
        {
            // Try another JSON serializer used by DataContract types.
            LogWarn($"System.Text.Json deserialize failed for {itemType.Name}. fallback=DataContractJsonSerializer");
        }

        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(rawJson));
            var serializer = new DataContractJsonSerializer(itemType, new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true,
            });
            return serializer.ReadObject(stream) as IItem;
        }
        catch
        {
            LogWarn($"DataContractJsonSerializer deserialize failed for {itemType.Name}.");
            return null;
        }
    }

    private static bool TryApplyRawProperties(object target, string rawJson, out int appliedPropertyCount, out string detail)
    {
        appliedPropertyCount = 0;
        detail = "no detail";
        if (string.IsNullOrWhiteSpace(rawJson))
            return false;

        try
        {
            var appliedNames = new List<string>();
            var failedNames = new List<string>();
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (string.Equals(target.GetType().Name, "ShapeItem", StringComparison.Ordinal))
            {
                TryApplyShapeSpecificProperties(target, root, ref appliedPropertyCount, appliedNames, failedNames);
            }

            var properties = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo? deferredShapeParameter = null;

            foreach (var property in properties)
            {
                if (!property.CanWrite)
                    continue;
                if (string.Equals(property.Name, "Frame", StringComparison.Ordinal) ||
                    string.Equals(property.Name, "Layer", StringComparison.Ordinal) ||
                    string.Equals(property.Name, "Length", StringComparison.Ordinal))
                    continue;
                if (string.Equals(property.Name, "ShapeType", StringComparison.Ordinal) ||
                    string.Equals(property.Name, "ShapeType2", StringComparison.Ordinal))
                    continue;
                if (string.Equals(property.Name, "ShapeParameter", StringComparison.Ordinal))
                {
                    // ShapeParameter often depends on shape type being set first.
                    deferredShapeParameter = property;
                    continue;
                }

                if (!TryGetJsonPropertyForTarget(root, property.Name, out var propertyElement))
                    continue;

                _ = ApplyProperty(target, property, propertyElement, appliedNames, failedNames, ref appliedPropertyCount);
            }

            if (deferredShapeParameter is not null &&
                TryGetJsonPropertyForTarget(root, deferredShapeParameter.Name, out var deferredElement))
            {
                var ok = ApplyProperty(target, deferredShapeParameter, deferredElement, appliedNames, failedNames, ref appliedPropertyCount);
                if (!ok && !failedNames.Contains(deferredShapeParameter.Name, StringComparer.Ordinal))
                    failedNames.Add(deferredShapeParameter.Name);
            }

            var appliedSummary = appliedNames.Count == 0
                ? "-"
                : string.Join(",", appliedNames.Take(12));
            var failedSummary = failedNames.Count == 0
                ? "-"
                : string.Join(",", failedNames.Take(12));
            detail = $"applied=[{appliedSummary}] failed=[{failedSummary}]";
            LogInfo($"TryApplyRawProperties target={target.GetType().Name} applied={appliedPropertyCount} failed={failedNames.Count} appliedNames=[{appliedSummary}] failedNames=[{failedSummary}]");
        }
        catch
        {
            LogWarn($"TryApplyRawProperties failed for {target.GetType().Name}.");
            return false;
        }

        return appliedPropertyCount > 0;
    }

    private static bool ApplyProperty(
        object target,
        PropertyInfo property,
        JsonElement propertyElement,
        List<string> appliedNames,
        List<string> failedNames,
        ref int appliedPropertyCount)
    {
        object? value;
        var ok = string.Equals(property.Name, "ShapeParameter", StringComparison.Ordinal)
            ? TryDeserializeShapeParameter(target, property, propertyElement, out value)
            : TryDeserializeElement(propertyElement, property.PropertyType, out value);
        if (!ok)
            return false;

        try
        {
            property.SetValue(target, value);
            appliedPropertyCount++;
            appliedNames.Add(property.Name);
            return true;
        }
        catch (Exception ex)
        {
            failedNames.Add(property.Name);
            LogWarn($"Property set failed. target={target.GetType().Name}, property={property.Name}, propertyType={property.PropertyType.FullName}, valueType={value?.GetType().FullName ?? "(null)"}, error={ex.Message}");
            return false;
        }
    }

    private static void TryApplyShapeSpecificProperties(
        object target,
        JsonElement root,
        ref int appliedPropertyCount,
        List<string> appliedNames,
        List<string> failedNames)
    {
        LogShapeMembersOnce(target);

        if (TryGetShapeTypeElement(root, out var shapeTypeElement))
        {
            if (TryApplyShapeType(target, shapeTypeElement))
            {
                appliedPropertyCount++;
                appliedNames.Add("ShapeType2");
            }
            else
            {
                failedNames.Add("ShapeType2");
            }
        }
    }

    private static void LogShapeMembersOnce(object target)
    {
        if (Interlocked.Exchange(ref shapeMembersLogged, 1) != 0)
            return;

        var type = target.GetType();
        var props = type
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.Name.Contains("Shape", StringComparison.OrdinalIgnoreCase))
            .Select(p => $"{p.Name}:{p.PropertyType.Name} [R={p.CanRead},W={p.CanWrite},Set={(p.SetMethod is null ? "N" : p.SetMethod.IsPublic ? "P" : "NP")}]");
        var methods = type
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Shape", StringComparison.OrdinalIgnoreCase) && m.GetParameters().Length <= 2)
            .Select(m => $"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");

        LogInfo($"ShapeItem members. type={type.FullName}, props=[{string.Join("; ", props.Take(24))}], methods=[{string.Join("; ", methods.Take(24))}]");
    }

    private static bool TryGetShapeTypeElement(JsonElement root, out JsonElement element)
    {
        if (root.TryGetProperty("ShapeType2", out element))
            return true;
        if (root.TryGetProperty("ShapeType", out element))
            return true;
        element = default;
        return false;
    }

    private static bool TryApplyShapeType(object target, JsonElement shapeTypeElement)
    {
        var candidates = new[] { "ShapeType2", "ShapeType" };
        foreach (var candidate in candidates)
        {
            if (TrySetMemberValue(target, candidate, shapeTypeElement, out var usedPath))
            {
                LogInfo($"ShapeType applied. member={candidate}, path={usedPath}");
                return true;
            }
        }

        var methods = target.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m =>
                (string.Equals(m.Name, "SetShapeType2", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(m.Name, "SetShapeType", StringComparison.OrdinalIgnoreCase) ||
                 (m.Name.Contains("ShapeType", StringComparison.OrdinalIgnoreCase) && m.GetParameters().Length == 1)))
            .ToArray();

        foreach (var method in methods)
        {
            var parameter = method.GetParameters().FirstOrDefault();
            if (parameter is null)
                continue;
            if (!TryConvertForTargetType(shapeTypeElement, parameter.ParameterType, out var arg))
                continue;
            try
            {
                method.Invoke(target, new[] { arg });
                LogInfo($"ShapeType applied. method={method.Name}, argType={arg?.GetType().FullName ?? "(null)"}");
                return true;
            }
            catch
            {
                // Try next method.
            }
        }

        LogWarn("ShapeType apply failed.");
        return false;
    }

    private static bool TrySetMemberValue(object target, string memberName, JsonElement element, out string usedPath)
    {
        usedPath = string.Empty;
        var type = target.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (property is not null && property.SetMethod is not null && TryConvertForTargetType(element, property.PropertyType, out var value))
        {
            try
            {
                property.SetValue(target, value);
                usedPath = property.SetMethod.IsPublic ? "property-public-setter" : "property-nonpublic-setter";
                return true;
            }
            catch
            {
                // fallback below
            }
        }

        var backingField = type.GetField($"<{memberName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        if (backingField is not null && TryConvertForTargetType(element, backingField.FieldType, out var backingValue))
        {
            try
            {
                backingField.SetValue(target, backingValue);
                usedPath = "backing-field";
                return true;
            }
            catch
            {
                // fallback below
            }
        }

        return false;
    }

    private static bool TryConvertForTargetType(JsonElement element, Type targetType, out object? value)
    {
        if (TryDeserializeElement(element, targetType, out value))
            return true;

        var raw = element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;
            return false;
        }

        var assignType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (assignType == typeof(string))
        {
            value = raw;
            return true;
        }

        if (assignType == typeof(Type))
        {
            value = ResolveType(raw);
            return value is not null;
        }

        var resolvedType = ResolveType(raw);
        if (resolvedType is null)
        {
            value = null;
            return false;
        }

        if (assignType.IsAssignableFrom(resolvedType))
        {
            try
            {
                value = Activator.CreateInstance(resolvedType, nonPublic: true);
                return value is not null;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        value = null;
        return false;
    }

    private static bool TryDeserializeShapeParameter(object target, PropertyInfo property, JsonElement element, out object? value)
    {
        value = null;

        // Prefer explicit type hint from .ymmt so polygon/triangle parameters are preserved.
        if (TryDeserializeUsingJsonTypeHint(element, property.PropertyType, out value))
            return true;

        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("$type", out var typeProperty) &&
            typeProperty.ValueKind == JsonValueKind.String)
        {
            var typeName = typeProperty.GetString();
            if (!string.IsNullOrWhiteSpace(typeName))
            {
                var hintedType = ResolveType(typeName);
                if (hintedType is not null && TryDeserializeElementAsType(element, hintedType, out value))
                {
                    LogInfo($"ShapeParameter deserialized using hinted type: {hintedType.FullName}");
                    return true;
                }
            }
        }

        try
        {
            var current = property.GetValue(target);
            if (current is not null)
            {
                var concreteType = current.GetType();
                if (TryDeserializeElementAsType(element, concreteType, out value))
                {
                    LogInfo($"ShapeParameter deserialized using current concrete type: {concreteType.FullName}");
                    return true;
                }

                if (element.ValueKind == JsonValueKind.Object &&
                    TryPatchObjectProperties(current, element, out var patchedCount))
                {
                    value = current;
                    LogInfo($"ShapeParameter patched in-place. type={concreteType.FullName}, patched={patchedCount}");
                    return true;
                }
            }
        }
        catch
        {
            // continue fallbacks
        }

        LogWarn($"ShapeParameter deserialize failed. propertyType={property.PropertyType.FullName}");
        return false;
    }

    private static bool TryPatchObjectProperties(object target, JsonElement source, out int patchedCount, int depth = 0)
    {
        patchedCount = 0;
        if (depth > 6 || source.ValueKind != JsonValueKind.Object)
            return false;

        var properties = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            if (property.GetIndexParameters().Length > 0)
                continue;
            if (string.Equals(property.Name, "$type", StringComparison.Ordinal))
                continue;
            if (!source.TryGetProperty(property.Name, out var propertyElement))
                continue;
            if (propertyElement.ValueKind == JsonValueKind.Null)
                continue;

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (propertyElement.ValueKind == JsonValueKind.Object && !IsSimpleType(propertyType))
            {
                var currentValue = property.GetValue(target);
                if (currentValue is not null && TryPatchObjectProperties(currentValue, propertyElement, out var nestedPatched, depth + 1))
                {
                    patchedCount += nestedPatched;
                    continue;
                }
            }
            if (propertyElement.ValueKind == JsonValueKind.Array)
            {
                var currentValue = property.GetValue(target);
                if (currentValue is System.Collections.IList list && TryPatchList(list, propertyElement, out var listPatched, depth + 1))
                {
                    patchedCount += listPatched;
                    continue;
                }
            }

            if (!property.CanWrite)
                continue;

            if (!TryDeserializeElement(propertyElement, property.PropertyType, out var converted))
                continue;

            try
            {
                property.SetValue(target, converted);
                patchedCount++;
            }
            catch
            {
                // Best effort.
            }
        }

        return patchedCount > 0;
    }

    private static bool TryPatchList(System.Collections.IList list, JsonElement sourceArray, out int patchedCount, int depth)
    {
        patchedCount = 0;
        if (depth > 6 || sourceArray.ValueKind != JsonValueKind.Array)
            return false;

        var max = Math.Min(list.Count, sourceArray.GetArrayLength());
        for (var i = 0; i < max; i++)
        {
            var src = sourceArray[i];
            var dst = list[i];
            if (dst is null)
                continue;

            if (src.ValueKind == JsonValueKind.Object && TryPatchObjectProperties(dst, src, out var nestedPatched, depth + 1))
            {
                patchedCount += nestedPatched;
            }
            else if (src.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                var valueType = dst.GetType();
                if (TryDeserializeElement(src, valueType, out var converted) && converted is not null)
                {
                    try
                    {
                        list[i] = converted;
                        patchedCount++;
                    }
                    catch
                    {
                        // keep best effort
                    }
                }
            }
        }

        return patchedCount > 0;
    }

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive
            || type.IsEnum
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(TimeSpan)
            || type == typeof(Guid);
    }

    private static bool TryGetJsonPropertyForTarget(JsonElement root, string propertyName, out JsonElement propertyElement)
    {
        if (root.TryGetProperty(propertyName, out propertyElement))
            return true;

        // YMM project JSON uses some legacy/suffixed property names.
        if (string.Equals(propertyName, "ShapeType", StringComparison.Ordinal) &&
            root.TryGetProperty("ShapeType2", out propertyElement))
            return true;

        if (string.Equals(propertyName, "ShapeType2", StringComparison.Ordinal) &&
            root.TryGetProperty("ShapeType", out propertyElement))
            return true;

        propertyElement = default;
        return false;
    }

    private static bool TryDeserializeElement(JsonElement element, Type targetType, out object? value)
    {
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (TryDeserializeUsingJsonTypeHint(element, targetType, out value))
            return true;

        try
        {
            value = JsonSerializer.Deserialize(element.GetRawText(), targetType, itemJsonSerializerOptions);
            if (value is not null)
                return true;
        }
        catch
        {
            // fallback below
        }

        if (underlyingType.IsInterface || underlyingType.IsAbstract)
        {
            value = null;
            return false;
        }

        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(element.GetRawText()));
            var serializer = new DataContractJsonSerializer(targetType, new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true,
            });
            value = serializer.ReadObject(stream);
            if (value is not null)
                return true;
        }
        catch
        {
            // fallback below
        }

        try
        {
            if (underlyingType.IsEnum)
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    var enumText = element.GetString();
                    if (!string.IsNullOrWhiteSpace(enumText))
                    {
                        value = Enum.Parse(underlyingType, enumText, ignoreCase: true);
                        return true;
                    }
                }
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var enumInt))
                {
                    value = Enum.ToObject(underlyingType, enumInt);
                    return true;
                }
            }
        }
        catch
        {
            // ignored
        }

        value = null;
        return false;
    }

    private static bool TryDeserializeElementAsType(JsonElement element, Type concreteType, out object? value)
    {
        value = null;

        try
        {
            value = JsonSerializer.Deserialize(element.GetRawText(), concreteType, itemJsonSerializerOptions);
            if (value is not null)
                return true;
        }
        catch
        {
            // fallback below
        }

        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(element.GetRawText()));
            var serializer = new DataContractJsonSerializer(concreteType, new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true,
            });
            value = serializer.ReadObject(stream);
            return value is not null;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDeserializeUsingJsonTypeHint(JsonElement element, Type targetType, out object? value)
    {
        value = null;
        if (element.ValueKind != JsonValueKind.Object)
            return false;
        if (!element.TryGetProperty("$type", out var typeProperty) || typeProperty.ValueKind != JsonValueKind.String)
            return false;

        var typeName = typeProperty.GetString();
        if (string.IsNullOrWhiteSpace(typeName))
            return false;

        var concreteType = ResolveType(typeName);
        if (concreteType is null)
            return false;

        var assignTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (!assignTarget.IsAssignableFrom(concreteType))
            return false;

        try
        {
            var viaStj = JsonSerializer.Deserialize(element.GetRawText(), concreteType, itemJsonSerializerOptions);
            if (viaStj is not null)
            {
                value = viaStj;
                return true;
            }
        }
        catch
        {
            // fallback below
        }

        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(element.GetRawText()));
            var serializer = new DataContractJsonSerializer(concreteType, new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true,
            });
            value = serializer.ReadObject(stream);
            return value is not null;
        }
        catch
        {
            return false;
        }
    }

    private static Type? ResolveType(string typeName)
    {
        var resolved = Type.GetType(typeName, throwOnError: false);
        if (resolved is not null)
            return resolved;

        var shortName = typeName.Split(',')[0].Trim();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                resolved = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
                if (resolved is not null)
                    return resolved;
                resolved = assembly.GetType(shortName, throwOnError: false, ignoreCase: false);
                if (resolved is not null)
                    return resolved;

                resolved = assembly
                    .GetTypes()
                    .FirstOrDefault(t =>
                        string.Equals(t.FullName, shortName, StringComparison.Ordinal) ||
                        string.Equals(t.Name, shortName, StringComparison.Ordinal));
                if (resolved is not null)
                    return resolved;
            }
            catch
            {
                // ignore broken assembly contexts
            }
        }

        return null;
    }

    private static void LogInfo(string message) => PluginLogger.Info(LogCategory, AttachOperation(message));
    private static void LogWarn(string message) => PluginLogger.Warn(LogCategory, AttachOperation(message));
    private static void LogError(string message, Exception? ex = null) => PluginLogger.Error(LogCategory, AttachOperation(message), ex);

    private static string AttachOperation(string message)
    {
        var op = currentOperationId.Value;
        return string.IsNullOrWhiteSpace(op) ? message : $"op={op} {message}";
    }

    private static string BuildSummary(YmmtProjectSnapshot snapshot, ImportResult importResult)
    {
        var sb = new StringBuilder();
        sb.AppendLine("YMMT load succeeded");
        sb.AppendLine($"Source: {snapshot.SourcePath}");
        sb.AppendLine($"Templates: {snapshot.Templates.Count}");
        sb.AppendLine($"Imported: {importResult.Imported}");
        sb.AppendLine($"Skipped: {importResult.Skipped}");
        if (!string.IsNullOrWhiteSpace(importResult.Note))
            sb.AppendLine($"Notes:\n{importResult.Note}");

        for (var i = 0; i < snapshot.Templates.Count; i++)
        {
            var template = snapshot.Templates[i];
            sb.AppendLine();
            sb.AppendLine($"[{i}] {template.Name} / Scene={template.SceneName} / {template.Width}x{template.Height} / FPS={template.Fps}");
            sb.AppendLine($"Items: {template.Items.Count}");

            var groups = template.Items
                .GroupBy(item => item.ItemType)
                .OrderByDescending(group => group.Count())
                .Select(group => $"{group.Key}:{group.Count()}");
            sb.AppendLine($"Types: {string.Join(", ", groups)}");
        }

        return sb.ToString();
    }

    private static string SaveToPluginFolder(string sourceYmmtPath)
    {
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppContext.BaseDirectory;
        var targetDirectory = Path.Combine(assemblyDirectory, "projects");
        Directory.CreateDirectory(targetDirectory);

        var fileName = Path.GetFileName(sourceYmmtPath);
        var destinationPath = Path.Combine(targetDirectory, fileName);

        if (!File.Exists(destinationPath))
        {
            File.Copy(sourceYmmtPath, destinationPath, overwrite: false);
            return destinationPath;
        }

        if (string.Equals(
                Path.GetFullPath(sourceYmmtPath),
                Path.GetFullPath(destinationPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return destinationPath;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var versionedPath = Path.Combine(targetDirectory, $"{baseName}_{timestamp}{extension}");
        File.Copy(sourceYmmtPath, versionedPath, overwrite: false);
        return versionedPath;
    }

    private readonly record struct ImportResult(int Imported, int Skipped, string Note);
}

