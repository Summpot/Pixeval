import re,plistlib
from pathlib import Path
s=Path('ios/Runner.xcodeproj/project.pbxproj').read_text()
pattern=r'\s+|/\*.*?\*/|//[^\n]*|"(?:\\.|[^"\\])*"|[{}()=;,]|[^\s{}()=;,]+'
tokens=[t for t in re.findall(pattern,s,re.S) if not t.isspace() and not t.startswith(('/*','//'))]
pos=0
def val():
 global pos
 t=tokens[pos];pos+=1
 if t=='{':
  d={}
  while tokens[pos]!='}':
   k=tokens[pos].strip('"');pos+=1
   assert tokens[pos]=='=';pos+=1
   assert k not in d,('duplicate key',k)
   d[k]=val();assert tokens[pos]==';';pos+=1
  pos+=1;return d
 if t=='(':
  a=[]
  while tokens[pos]!=')':
   a.append(val())
   if tokens[pos]==',':pos+=1
  pos+=1;return a
 return t.strip('"')
p=val();assert pos==len(tokens)
objects=p['objects']; project=objects[p['rootObject']]
runner=next(v for v in objects.values() if v.get('isa')=='PBXNativeTarget' and v['name']=='Runner')
share=next(v for v in objects.values() if v.get('isa')=='PBXNativeTarget' and v['name']=='PixevalShare')
assert any(objects[d].get('target') in project['targets'] and objects[objects[d]['target']]['name']=='PixevalShare' for d in runner['dependencies'])
assert any(objects[phase]['isa']=='PBXCopyFilesBuildPhase' and objects[phase].get('dstSubfolderSpec')=='13' and any(objects[f]['fileRef']==share['productReference'] for f in objects[phase]['files']) for phase in runner['buildPhases'])
for target in [runner,share]:
 cfgs=objects[target['buildConfigurationList']]['buildConfigurations'];assert len(cfgs)==3
 for cfg in cfgs:
  settings=objects[cfg]['buildSettings'];assert settings['PIXEVAL_APP_GROUP']=='group.io.github.pixeval.pixevalFlutter'
  with Path('ios',settings['CODE_SIGN_ENTITLEMENTS']).open('rb') as f:assert 'com.apple.security.application-groups' in plistlib.load(f)
  with Path('ios',settings['INFOPLIST_FILE']).open('rb') as f:assert plistlib.load(f)['PixevalAppGroup']=='$(PIXEVAL_APP_GROUP)'
 for phase in target['buildPhases']:
  if objects[phase]['isa']=='PBXSourcesBuildPhase':
   for f in objects[phase]['files']:
    ref=objects[objects[f]['fileRef']];path=ref['path']
    if '/' in path and not path.startswith('Flutter') and path.endswith('.swift'): assert Path('ios',path).exists(),path
print('Xcode project parsed; share target/embed/dependency/sources and 6 App Group configurations verified')
