"""Build a local VSIX without npm dependencies or network access."""
from pathlib import Path
import json
import zipfile
import hashlib
from xml.sax.saxutils import escape

source = Path(__file__).resolve().parents[1]
package = json.loads((source / 'package.json').read_text(encoding='utf-8'))
messages = json.loads((source / 'package.nls.zh-cn.json').read_text(encoding='utf-8'))
def metadata(value):
    return messages.get(value[1:-1], value) if value.startswith('%') and value.endswith('%') else value
name, version = package['name'], package['version']
destination = source.parent / f'{name}-{version}.vsix'
assert (source / 'desktop/CodeWhip.Desktop.exe').is_file(), 'Build desktop component first'
manifest = f'''<?xml version="1.0" encoding="utf-8"?>
<PackageManifest Version="2.0.0" xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011" xmlns:d="http://schemas.microsoft.com/developer/vsx-schema-design/2011">
 <Metadata>
  <Identity Id="{escape(name)}" Version="{escape(version)}" Language="en-US" Publisher="{escape(package['publisher'])}" />
  <DisplayName>{escape(metadata(package['displayName']))}</DisplayName>
  <Description xml:space="preserve">{escape(metadata(package['description']))}</Description>
  <Tags>whip,syntax,animation,fun,windows</Tags><Categories>Other</Categories><GalleryFlags>Public</GalleryFlags>
  <Properties>
   <Property Id="Microsoft.VisualStudio.Code.Engine" Value="{escape(package['engines']['vscode'])}" />
   <Property Id="Microsoft.VisualStudio.Code.ExtensionKind" Value="ui" />
  </Properties>
  <License>extension/LICENSE</License><Icon>extension/media/icon.png</Icon>
 </Metadata>
 <Installation><InstallationTarget Id="Microsoft.VisualStudio.Code" /></Installation>
 <Dependencies />
 <Assets>
  <Asset Type="Microsoft.VisualStudio.Code.Manifest" Path="extension/package.json" Addressable="true" />
  <Asset Type="Microsoft.VisualStudio.Services.Content.Details" Path="extension/README.md" Addressable="true" />
  
  <Asset Type="Microsoft.VisualStudio.Services.Content.License" Path="extension/LICENSE" Addressable="true" />
  <Asset Type="Microsoft.VisualStudio.Services.Icons.Default" Path="extension/media/icon.png" Addressable="true" />
 </Assets>
</PackageManifest>'''
content_types='''<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
 <Default Extension="json" ContentType="application/json" />
 <Default Extension="js" ContentType="application/javascript" />
 <Default Extension="html" ContentType="text/html" />
 <Default Extension="png" ContentType="image/png" />
 <Default Extension="exe" ContentType="application/octet-stream" />
 <Default Extension="txt" ContentType="text/plain" />
 <Default Extension="md" ContentType="text/markdown" />
 <Default Extension="vsixmanifest" ContentType="text/xml" />
 <Override PartName="/extension/LICENSE" ContentType="text/plain" />
 <Override PartName="/extension/NOTICE" ContentType="text/plain" />
</Types>'''
files=[]
for file in sorted(source.rglob('*')):
    if not file.is_file(): continue
    rel=file.relative_to(source).as_posix()
    if rel.startswith(('src/','media/')) or rel=='desktop/CodeWhip.Desktop.exe' or '/' not in rel:
        files.append(file)
with zipfile.ZipFile(destination,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as archive:
    archive.writestr('[Content_Types].xml',content_types)
    archive.writestr('extension.vsixmanifest',manifest)
    for file in files:archive.write(file,'extension/'+file.relative_to(source).as_posix())
with zipfile.ZipFile(destination) as archive:
    assert archive.testzip() is None
    assert json.loads(archive.read('extension/package.json'))['version']==version
digest=hashlib.sha256(destination.read_bytes()).hexdigest()
(destination.with_suffix('.vsix.sha256')).write_text(digest+'  '+destination.name+'\n',encoding='utf-8')
print(f'{destination} ({destination.stat().st_size:,} bytes; {len(files)} files)')
print('SHA256 '+digest)
