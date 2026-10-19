'use strict';
const vscode=require('vscode'),path=require('path'),fs=require('fs'),cp=require('child_process'),readline=require('readline');
const locales=require('../media/locales.json');
const {DiagnosticLedger,clean}=require('./diagnostics');
const {comment,commentText}=require('./messages');
const schema=require('../package.json').contributes.configuration.properties;
let context,output,status,decoration,panel,helper,ready=false,paused=false,disposed=false;
const ledger=new DiagnosticLedger(),pending=new Map(),hiddenComments=new Set();
const counters={strikes:0};
const config=()=>vscode.workspace.getConfiguration('codeWhip');
function language(){const value=config().get('language','auto');if(value!=='auto')return locales[value]?value:'en';const system=vscode.env.language.toLowerCase();return system.startsWith('zh')?'zh-CN':Object.keys(locales).find(k=>system.startsWith(k))||'en';}
const t=key=>locales[language()].ui[key]||locales.en.ui[key]||key;
const enabled=()=>config().get('enabled',true)&&!paused;
function settings(){const c=config();return {enabled:c.get('enabled',true),language:c.get('language','auto'),material:c.get('material','leather'),intensity:c.get('intensity','light'),sound:c.get('sound',true),volume:c.get('volume',.25),showComments:c.get('showComments',true),reducedMotion:c.get('reducedMotion',false)};}
function usable(doc){return doc&&!doc.isClosed&&['file','untitled'].includes(doc.uri.scheme);}
function errors(doc){return vscode.languages.getDiagnostics(doc.uri).filter(d=>d.severity===vscode.DiagnosticSeverity.Error).map(d=>({code:typeof d.code==='object'?d.code.value:d.code??'',source:d.source||'',message:d.message,line:d.range.start.line,column:d.range.start.character,endLine:d.range.end.line,endColumn:d.range.end.character,token:doc.getText(d.range).trim()}));}
function refresh(){
  if(disposed)return;
  const doc=vscode.window.activeTextEditor?.document,count=usable(doc)?(ledger.files.get(doc.uri.toString())||[]).length:0;
  status.text=`$(zap) ${t('name')}: ${!enabled()?t('paused'):!ready?t('offline'):t('watching')}${count?' · '+count+' '+t('errors'):''}`;
  status.tooltip=t('subtitle');status.command='codeWhip.settings';status.show();
  panel?.webview.postMessage({type:'state',settings:settings(),ui:locales[language()].ui,language:language(),paused,ready});
}
function render(editor){
  if(!editor||!decoration)return;
  const doc=editor.document,key=doc.uri.toString(),all=ledger.files.get(key)||[];
  if(!enabled()||!settings().showComments||hiddenComments.has(key)){editor.setDecorations(decoration,[]);return;}
  const lines=new Map();for(const error of all){const line=Math.max(0,Math.min(error.line,doc.lineCount-1));if(!lines.has(line))lines.set(line,[]);lines.get(line).push(error);}
  editor.setDecorations(decoration,[...lines].map(([line,group])=>{
    const words=comment(group[0],language()),text=commentText(words,doc.languageId),hover=new vscode.MarkdownString();
    for(const error of group)hover.appendText(comment(error,language())+'\n'+clean(error.source)+' '+error.code+'\n'+clean(error.message)+'\n\n');
    return {range:new vscode.Range(line,doc.lineAt(line).text.length,line,doc.lineAt(line).text.length),hoverMessage:hover,renderOptions:{after:{contentText:text.length>220?text.slice(0,217)+'…':text}}};
  }));
}
function write(event){if(!ready||!helper)return false;try{helper.stdin.write(JSON.stringify({...event,settings:settings()})+'\n',error=>{if(error)output.appendLine(error.message);});if(event.kind==='strike')counters.strikes++;return true;}catch(error){output.appendLine(error.message);return false;}}
function dispatch(doc){
  const key=doc.uri.toString();
  if(!ready){pending.set(key,doc);startHelper();return;}
  write({kind:'strike',id:key});
}
function flush(){for(const [key,doc] of pending){pending.delete(key);if(!enabled()||!usable(doc)||vscode.window.activeTextEditor?.document.uri.toString()!==key)continue;if(errors(doc).length)write({kind:'strike',id:key});}}
function startHelper(){
  if(helper||disposed||process.platform!=='win32')return;
  const child=cp.spawn(path.join(context.extensionPath,'desktop','CodeWhip.Desktop.exe'),[],{windowsHide:true,stdio:['pipe','pipe','pipe']});helper=child;
  child.stdin.on('error',error=>output.appendLine(error.message));child.stderr.on('data',data=>output.appendLine(String(data)));
  readline.createInterface({input:child.stdout}).on('line',line=>{try{const event=JSON.parse(line);if(helper!==child)return;if(event.action==='ready'){ready=true;flush();refresh();}if(event.action==='error')output.appendLine(event.message);}catch{output.appendLine(line);}});
  const ended=error=>{if(helper!==child)return;helper=undefined;ready=false;if(error)output.appendLine(String(error));refresh();};child.on('error',ended);child.on('exit',()=>ended());
}
function stopHelper(){const child=helper;helper=undefined;ready=false;if(child){try{child.stdin.end('{"kind":"quit"}\n');}catch{}setTimeout(()=>{if(child.exitCode===null)child.kill();},1000).unref();}}
function sync(doc,silent=false){
  if(!usable(doc))return;
  const editor=vscode.window.activeTextEditor;if(!editor||editor.document.uri.toString()!==doc.uri.toString())return;
  const key=doc.uri.toString(),current=errors(doc),change=ledger.update(key,current);
  if(change.added.length||change.cleared)hiddenComments.delete(key);
  if(!current.length)pending.delete(key);
  render(editor);refresh();
  if(silent||!enabled())return;
  if(change.added.length){output.appendLine(comment(change.added[0],language()));dispatch(doc);}

}
function togglePause(){paused=!paused;pending.clear();if(paused)write({kind:'stop'});const editor=vscode.window.activeTextEditor;if(editor)sync(editor.document,true);refresh();}
async function preview(){startHelper();for(let i=0;i<30&&!ready;i++)await new Promise(resolve=>setTimeout(resolve,100));if(!write({kind:'strike'}))vscode.window.showInformationMessage(t('offline'));}
function validate(values){const valid={};for(const [key,value] of Object.entries(values||{})){const rule=schema['codeWhip.'+key];if(!rule)continue;if(typeof value!==rule.type||rule.type==='number'&&!Number.isFinite(value)||rule.enum&&!rule.enum.includes(value)||rule.minimum!==undefined&&value<rule.minimum||rule.maximum!==undefined&&value>rule.maximum)throw Error(key);valid[key]=value;}return valid;}
function openSettings(){
  if(panel){panel.reveal();refresh();return;}
  panel=vscode.window.createWebviewPanel('codeWhipSettings',t('name'),vscode.ViewColumn.One,{enableScripts:true,localResourceRoots:[]});
  const nonce=require('crypto').randomBytes(16).toString('hex');panel.webview.html=fs.readFileSync(path.join(context.extensionPath,'media','settings.html'),'utf8').replaceAll('{{nonce}}',nonce);
  panel.onDidDispose(()=>panel=undefined);panel.webview.onDidReceiveMessage(async message=>{try{
    if(message.type==='ready')refresh();
    if(message.type==='preview')await preview();
    if(message.type==='save'){for(const [key,value] of Object.entries(validate(message.settings)))if(config().get(key)!==value)await config().update(key,value,vscode.ConfigurationTarget.Global);panel?.webview.postMessage({type:'saved'});refresh();}
  }catch(error){vscode.window.showErrorMessage(t('invalid')+': '+error.message);}});
}
function clearComments(){const editor=vscode.window.activeTextEditor;if(editor){hiddenComments.add(editor.document.uri.toString());render(editor);}}
async function activate(ctx){
  context=ctx;disposed=false;output=vscode.window.createOutputChannel('Code Whip');status=vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right,30);
  for(const key of ['scope','tone','customPhrases','flash','trails','commentMode','repairSound'])if(config().inspect(key)?.globalValue!==undefined)await config().update(key,undefined,vscode.ConfigurationTarget.Global);
  decoration=vscode.window.createTextEditorDecorationType({rangeBehavior:vscode.DecorationRangeBehavior.ClosedClosed,after:{color:new vscode.ThemeColor('editorCodeLens.foreground'),fontStyle:'italic',margin:'0 0 0 1.8em'}});
  ctx.subscriptions.push(output,status,decoration,
    vscode.commands.registerCommand('codeWhip.settings',openSettings),vscode.commands.registerCommand('codeWhip.preview',preview),
    vscode.commands.registerCommand('codeWhip.pause',togglePause),vscode.commands.registerCommand('codeWhip.clearComments',clearComments),vscode.commands.registerCommand('codeWhip.restart',()=>{stopHelper();startHelper();}),
    vscode.languages.onDidChangeDiagnostics(event=>{const doc=vscode.window.activeTextEditor?.document;if(doc&&event.uris.some(uri=>uri.toString()===doc.uri.toString()))sync(doc);}),
    vscode.window.onDidChangeActiveTextEditor(editor=>{if(editor)sync(editor.document);else refresh();}),
    vscode.window.onDidChangeVisibleTextEditors(editors=>editors.forEach(render)),
    vscode.workspace.onDidCloseTextDocument(doc=>{const key=doc.uri.toString();ledger.clear(key);pending.delete(key);hiddenComments.delete(key);}),
    vscode.workspace.onDidChangeConfiguration(event=>{if(!event.affectsConfiguration('codeWhip'))return;pending.clear();if(!enabled())write({kind:'stop'});vscode.window.visibleTextEditors.forEach(render);refresh();})
  );
  startHelper();const editor=vscode.window.activeTextEditor;if(editor)sync(editor.document);refresh();
  return {getState:()=>({ready,paused,counters:{...counters},settings:settings(),files:[...ledger.files].map(([uri,list])=>({uri,errors:list.map(e=>({...e,comment:comment(e,language())}))}))})};
}
function deactivate(){disposed=true;pending.clear();stopHelper();}
module.exports={activate,deactivate};
