"""One-shot migration from the pre-ZeroI18n resource tree. Run from the repository root.

The checked-in manifest and test baseline are the audit trail; this script is not a
build dependency. Composite-format strings are parsed, including escaped braces.
"""
import json
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
APP = ROOT / 'src/Pixeval'

def flatten(value, path):
    if isinstance(value, dict):
        for k, v in value.items():
            yield from flatten(v, path + '.' + k)
    elif isinstance(value, list):
        for i, v in enumerate(value):
            yield from flatten(v, f'{path}[{i}]')
    else:
        yield path, str(value) if value is not None else ''

def parts(text):
    literal = ''
    i = 0
    while i < len(text):
        if text[i:i+2] in ('{{', '}}'):
            literal += text[i]
            i += 2
        elif text[i] == '{' and i+1 < len(text) and text[i+1].isdigit():
            if literal:
                yield literal
                literal = ''
            end = text.index('}', i)
            match = re.fullmatch(r'(\d+)(?:,(-?\d+))?(?::(.*))?', text[i+1:end])
            if not match:
                raise ValueError(text)
            yield int(match[1]), match[2], match[3]
            i = end + 1
        else:
            literal += text[i]
            i += 1
    if literal:
        yield literal

def names(key, count):
    tail = key.split('.')[-1]
    if key.startswith('Filter.Diagnostics.'):
        if tail == 'CompletionSuffixFormatted': return ['completions']
        if tail == 'MessageWithPositionFormatted': return ['message', 'position', 'completionSuffix']
        if 'RangeMinimum' in tail: return ['predicate', 'minimum', 'maximum']
        if count == 2: return ['predicate', 'value']
        return ['value']
    if key.startswith('MacroParser.'):
        if tail == 'MessageWithPositionFormatted': return ['message', 'position']
        return ['macroName', 'formatter'][:count] if 'Token' not in tail else ['token']
    if key.startswith('NavigationYamlParser.'):
        if 'FieldNotAllowed' in tail: return ['item', 'field']
        if 'MaxDepth' in tail: return ['item', 'maxDepth']
        return ['value']
    if 'RateLimit' in key: return ['count', 'retryAt'] if count == 2 else ['retryAt']
    if 'McpToolsStatus' in key: return ['endpoint', 'count'][:count] if tail != 'Failed' else ['error']
    if 'FeedPage.' in key: return ['name', 'count'][:count]
    if 'SearchResult' in key: return ['searchText']
    if 'ErrorMessage' in key: return ['error']
    if 'Progress' in key or 'Running' in key: return ['progress']
    if 'Version' in key or 'OutdatedFormatted' in key and 'ExtensionOutdated' in key or 'ReadyFormatted' in key or 'TitleFormatted' in key: return ['version']
    if 'GoToPage' in key: return ['page']
    if 'MacroInvalid' in key: return ['message']
    return ['count'] if count == 1 else [f'value{i+1}' for i in range(count)]

def method(key):
    return ''.join(p[0].upper()+p[1:] for p in re.split(r'[^\w]+', key) if p)

def fluent_literal(s):
    # Preserve whitespace and literal braces with Fluent string expressions.
    if not s: return ''
    if any(c in s for c in '{}\n\r\t') or s != s.strip():
        escaped = s.replace('\\', '\\\\').replace('"', '\\"')
        escaped = escaped.replace('\n', '\\u000A').replace('\r', '\\u000D').replace('\t', '\\u0009')
        return '{ "' + escaped + '" }'
    return s

def split_args(s):
    result, start, stack, quote, i = [], 0, [], None, 0
    while i < len(s):
        c = s[i]
        if quote:
            if c == '\\': i += 2; continue
            if c == quote: quote = None
        elif c in '\"\'': quote = c
        elif c in '([{': stack.append(c)
        elif c in ')]}':
            if not stack: return result + [s[start:i].strip()], i
            stack.pop()
        elif c == ',' and not stack:
            result.append(s[start:i].strip()); start = i + 1
        i += 1
    return result + [s[start:].strip()], i

def main():
    baseline, manifest = {}, {}
    for folder in sorted((APP / 'i18n').iterdir()):
        if not folder.is_dir(): continue
        baseline[folder.name] = {}
        for file in sorted(folder.glob('*.json')):
            data = dict(flatten(json.loads(file.read_text(encoding='utf-8-sig')), file.stem))
            baseline[folder.name].update(data)
            for key, text in data.items():
                # Only formatted resources have composite-format brace escaping.
                tokens = list(parts(text)) if re.search(r'\{\d', text) else [text]
                indexes = [p[0] for p in tokens if isinstance(p, tuple)]
                count = max(indexes, default=-1) + 1
                if key not in manifest or count > len(manifest[key]['parameters']):
                    manifest[key] = {'key': key.replace('.', '-'), 'method': method(key),
                                     'parameters': names(key, count) if count else [],
                                     'formats': {str(p[0]): p[2] for p in tokens if isinstance(p, tuple) and p[2]}}
        for file in folder.glob('*.md'):
            target = APP / 'Assets/Help' / folder.name / file.name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(file, target)
    for locale, data in baseline.items():
        groups = {}
        for key, text in data.items():
            entry = manifest[key]
            tokens = list(parts(text)) if re.search(r'\{\d', text) else [text]
            pattern = ''.join(fluent_literal(p) if isinstance(p, str) else '{ $' + entry['parameters'][p[0]] + ' }' for p in tokens)
            # Keep the explicit product-behavior change separate from the baseline.
            if key == 'AppSettings.AppLanguageEntry.Header':
                pattern = pattern.replace('（重启应用后生效）', '').replace(' (restart required)', '').replace(' (requires restart)', '')
            group, rest = key.split('.', 1)
            groups.setdefault(group, []).append(rest.replace('.', '-') + ' = ' + (pattern or '{ "" }'))
        for group, lines in groups.items():
            target = APP / 'messages' / group / (locale + '.ftl')
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text('\n\n'.join(lines) + '\n', encoding='utf-8')
    (ROOT / 'scripts/i18n/mapping.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    fixture = ROOT / 'src/Pixeval.Tests/Fixtures/Localization/baseline.json'
    fixture.parent.mkdir(parents=True, exist_ok=True)
    fixture.write_text(json.dumps(baseline, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    refs = {k.split('.')[0]+'Resources.'+k.split('.',1)[1]: k for k in manifest}
    ref_pattern = re.compile(r'\b[A-Za-z]+Resources(?:\.[A-Za-z_][A-Za-z0-9_]*)+')
    def replace_call(args):
        key = refs.get(args[0])
        if key is None: return 'Strings.GetString(' + ', '.join(args) + ')'
        entry = manifest[key]
        values = args[1:]
        if values and values[0] == '[.. diagnostic.Arguments]':
            values = [f'diagnostic.Arguments[{i}]' for i in range(len(entry['parameters']))]
        if len(values) != len(entry['parameters']):
            return 'Strings.GetString(' + ', '.join(args) + ')'
        formatted = []
        for i, value in enumerate(values):
            if str(i) in entry['formats']:
                value = '(' + value + ').ToString(' + json.dumps(entry['formats'][str(i)]) + ', AppLocalization.Instance.CurrentCulture)'
            formatted.append(entry['parameters'][i] + ': ' + value)
        return 'Strings.' + entry['method'] + '(' + ', '.join(formatted) + ')'
    for root in [APP, ROOT/'src/Pixeval.Tests']:
        for file in root.rglob('*.cs'):
            if any(p in file.parts for p in ('obj','bin','i18n')): continue
            text = file.read_text(encoding='utf-8-sig')
            # Flatten the legacy two-step string.Format + lookup before conversion.
            pattern = re.compile(r'string\.Format\(\s*I18NManager\.GetResource\(')
            while m := pattern.search(text):
                inner, end = split_args(text[m.end():])
                after = m.end()+end+1
                outer, end2 = split_args(text[after:])
                extra = [a for a in outer if a]
                replacement = replace_call(inner + extra)
                text = text[:m.start()] + replacement + text[after+end2+1:]
            pos = 0
            while (start := text.find('I18NManager.GetResource(', pos)) >= 0:
                begin = start + len('I18NManager.GetResource(')
                args, length = split_args(text[begin:])
                replacement = replace_call(args)
                text = text[:start]+replacement+text[begin+length+1:]
                pos = start+len(replacement)
            text = ref_pattern.sub(lambda m: 'nameof(Strings.'+manifest[refs[m[0]]]['method']+')' if m[0] in refs else m[0], text)
            text = text.replace('using Pixeval.I18N;', 'using Pixeval.Localization;')
            file.write_text(text, encoding='utf-8')
    # Resource references in markup become live bindings, without a markup lookup shim.
    for file in APP.rglob('*.axaml'):
        if any(p in file.parts for p in ('obj','bin')): continue
        text = file.read_text(encoding='utf-8-sig')
        def markup(m):
            ref = m[1].replace('+', '.')
            if ref.startswith('MarkdownResources.'):
                return '{ReflectionBinding ['+ref.split('.',1)[1]+'], Source={x:Static loc:LocalizedHelp.Instance}}'
            key = refs[ref]
            return '{ReflectionBinding ['+manifest[key]['key']+'], Source={x:Static loc:LocalizationManager.Instance}}'
        text, count = re.subn(r'\{I18N \{x:Static \w+:([^}]+)\}\}', markup, text)
        if count:
            text = text.replace('xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"', 'xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"\n    xmlns:loc="using:Pixeval.Localization"')
        file.write_text(text, encoding='utf-8')
    aliases = []
    for key, entry in manifest.items():
        if not entry['parameters']:
            aliases.append('        '+json.dumps(key)+' => nameof(Strings.'+entry['method']+'),')
    target = APP / 'Localization/MessageKeys.cs'
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text('// Generated from scripts/i18n/mapping.json; preserves persisted navigation keys.\nnamespace Pixeval.Localization;\n\npublic static class MessageKeys\n{\n    public static string? Resolve(string key) => key switch\n    {\n'+'\n'.join(aliases)+'\n        _ => null\n    };\n\n    public static bool TryGet(string key, out string value)\n    {\n        var resolved = Resolve(key);\n        value = resolved is null ? key : Strings.GetString(resolved);\n        return resolved is not null;\n    }\n}\n', encoding='utf-8')

if __name__ == '__main__': main()
