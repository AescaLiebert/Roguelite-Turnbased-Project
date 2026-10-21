using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using Newtonsoft.Json;

// Deliberately targets these three five-asset packages only. No Unity roster rebuild,
// object references, artwork fields, GUIDs, source text or authoring graphs are rewritten.
var root = Path.GetFullPath(args.FirstOrDefault(argument => argument != "--check") ?? Directory.GetCurrentDirectory());
var check = args.Contains("--check");
var folder = Path.Combine(root, "Assets/Project/Data/Character/Pre-Release_Phase/PR_Genshin-Impact");
var pending = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (var slug in new[] { "fischl", "xiangling", "ningguang" })
{
    var metadataPath = Path.Combine(folder, slug + ".asset");
    var metadataText = File.ReadAllText(metadataPath);
    var character = Read<CharacterDefinition>(metadataText, "studioMetadataJson");
    if (character.Id != "fighter.pre-release." + slug) throw new InvalidDataException("Unexpected character: " + metadataPath);
    character.Skills = new List<SkillDefinition>();
    var packageFiles = new Dictionary<int, (string path, string text)>();
    foreach (var slot in new[] { 1, 2 })
    {
        var path = Path.Combine(folder, slug + "_skilldata" + slot + ".asset");
        var text = File.ReadAllText(path);
        packageFiles.Add(slot, (path, text));
        character.Skills.Add(Read<SkillDefinition>(text, "studioDefinitionJson"));
    }
    var ultimatePath = Path.Combine(folder, slug + "_ultdata1.asset");
    var ultimateText = File.ReadAllText(ultimatePath);
    character.UltimateTiers = Read<List<UltimateTierDefinition>>(ultimateText, "studioDefinitionJson");
    var passivePath = Path.Combine(folder, slug + "_Passive.asset");
    var passiveText = File.ReadAllText(passivePath);
    character.Passive = Read<PassiveDefinition>(passiveText, "studioDefinitionJson");
    GenshinSummonKits.Upgrade(character);
    foreach (var skill in character.Skills)
    {
        var file = packageFiles[skill.Slot];
        pending.Add(file.path, Replace(file.text, "studioDefinitionJson", skill));
    }
    pending.Add(ultimatePath, Replace(ultimateText, "studioDefinitionJson", character.UltimateTiers));
    pending.Add(passivePath, Replace(passiveText, "studioDefinitionJson", character.Passive));

    // Metadata intentionally owns no native card/passive graph. Preserve that separation.
    var metadata = Read<CharacterDefinition>(metadataText, "studioMetadataJson");
    metadata.ReviewNotes = character.ReviewNotes;
    pending.Add(metadataPath, Replace(metadataText, "studioMetadataJson", metadata));

    // Validate the actual persisted package before any file is written. Reopening a native
    // Studio package reattaches these native definitions to its saved authoring document.
    var document = StudioDocumentService.ImportCharacter(character);
    var compiled = StudioCompiler.Compile(document, DescriptionLibrary.CreateOpeningGaugeLibrary());
    var errors = compiled.Diagnostics.Where(diagnostic => diagnostic.Severity == StudioDiagnosticSeverity.Error).ToList();
    if (errors.Count != 0) throw new InvalidDataException(slug + ": " + string.Join("; ", errors));
}

var changed = pending.Where(item => File.ReadAllText(item.Key) != item.Value).ToList();
foreach (var item in changed)
{
    if (!check) File.WriteAllText(item.Key, item.Value, new UTF8Encoding(false));
    Console.WriteLine((check ? "Needs migration: " : "Migrated: ") + Path.GetRelativePath(root, item.Key));
}
Console.WriteLine($"{(check ? "Check" : "Migration")} complete: {changed.Count} of {pending.Count} targeted files differ.");
Environment.ExitCode = check && changed.Count != 0 ? 1 : 0;

static T Read<T>(string text, string field)
{
    var scalar = Scalar(text, field).Groups["value"].Value;
    // Unity wraps quoted strings across indented YAML lines; YAML folds those to spaces.
    scalar = Regex.Replace(scalar, @"\\\r?\n[ ]+", "");
    scalar = Regex.Replace(scalar, @"\r?\n[ ]+", " ").Trim();
    string json;
    if (scalar.StartsWith('\'')) json = scalar[1..^1].Replace("''", "'");
    else if (scalar.StartsWith('"')) json = DecodeDoubleQuoted(scalar);
    else throw new InvalidDataException("Expected quoted Unity YAML scalar: " + field);
    return SnapshotJson.Deserialize<T>(json) ?? throw new InvalidDataException("Missing " + field);
}

static Match Scalar(string text, string field)
{
    var match = Regex.Match(text, @"(?m)^  " + Regex.Escape(field) + @": (?<value>[^\r\n]*(?:\r?\n    [^\r\n]*)*)");
    if (!match.Success) throw new InvalidDataException("Missing Unity YAML field: " + field);
    return match;
}

static string Replace<T>(string text, string field, T value)
{
    var match = Scalar(text, field);
    var scalar = JsonConvert.SerializeObject(SnapshotJson.Serialize(value), new JsonSerializerSettings {
        StringEscapeHandling = StringEscapeHandling.EscapeNonAscii });
    return text[..match.Index] + "  " + field + ": " + scalar + text[(match.Index + match.Length)..];
}

static string DecodeDoubleQuoted(string scalar)
{
    if (!scalar.EndsWith('"')) throw new InvalidDataException("Unterminated Unity YAML quoted scalar.");
    var result = new StringBuilder();
    for (var index = 1; index < scalar.Length - 1; index++)
    {
        var character = scalar[index];
        if (character != '\\') { result.Append(character); continue; }
        var escape = scalar[++index];
        if (escape is 'x' or 'u' or 'U')
        {
            var digits = escape == 'x' ? 2 : escape == 'u' ? 4 : 8;
            var codepoint = int.Parse(scalar.Substring(index + 1, digits), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            result.Append(char.ConvertFromUtf32(codepoint)); index += digits;
        }
        else result.Append(escape switch { '0' => '\0', 'a' => '\a', 'b' => '\b', 't' => '\t',
            'n' => '\n', 'v' => '\v', 'f' => '\f', 'r' => '\r', 'e' => '\x1B',
            'N' => '\u0085', '_' => '\u00A0', 'L' => '\u2028', 'P' => '\u2029',
            ' ' => ' ', '\\' => '\\', '"' => '"', '/' => '/', _ => throw new InvalidDataException("Unsupported YAML escape: " + escape) });
    }
    return result.ToString();
}
