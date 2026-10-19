'use strict';
const vscode=require('vscode'),assert=require('node:assert/strict'),fs=require('fs'),path=require('path');
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
async function until(fn,label,timeout=15000){for(let t=0;t<timeout;t+=50){if(fn())return;await sleep(50);}throw Error('Timed out: '+label);}
async function run(){
  const result=path.join(vscode.workspace.workspaceFolders[0].uri.fsPath,'../vscode-direct-result.json');
  try{
    const c=vscode.workspace.getConfiguration('codeWhip');for(const [key,value] of Object.entries({enabled:true,language:'zh-CN',showComments:true,sound:false,reducedMotion:true}))await c.update(key,value,vscode.ConfigurationTarget.Global);
    const api=await vscode.extensions.getExtension('local-farm-tools.code-whip').activate();await until(()=>api.getState().ready,'native helper');
    async function set(doc,text){const editor=await vscode.window.showTextDocument(doc);assert.ok(await editor.edit(e=>e.replace(new vscode.Range(doc.positionAt(0),doc.positionAt(doc.getText().length)),text)));}
    const doc=await vscode.workspace.openTextDocument({language:'typescript',content:''});await vscode.window.showTextDocument(doc);
    let maxLag=0;const subscription=vscode.languages.onDidChangeDiagnostics(event=>{if(event.uris.some(uri=>uri.toString()===doc.uri.toString()))doc.lastMarkAt=Date.now();});
    for(let round=0;round<4;round++){
      await set(doc,'const count: number = "hello";');const version=doc.version;
      await until(()=>api.getState().counters.strikes===round+1,'red appearance '+round);
      maxLag=Math.max(maxLag,Date.now()-(doc.lastMarkAt||Date.now()));assert.equal(doc.version,version);assert.ok(doc.isUntitled);
      const state=api.getState().files.find(file=>file.uri===doc.uri.toString());assert.ok(state.errors[0].comment.includes('你他妈怎么蠢得跟猪一样'));assert.ok(state.errors[0].comment.includes('第1行'));assert.ok(state.errors[0].comment.includes('类型'));
      await set(doc,'const count: number = 1;');await until(()=>api.getState().files.find(file=>file.uri===doc.uri.toString()).errors.length===0,'red disappearance '+round);await sleep(100);assert.deepEqual(api.getState().counters,{strikes:round+1});
      assert.equal(api.getState().files.find(file=>file.uri===doc.uri.toString()).errors.length,0);
    }
    subscription.dispose();assert.ok(maxLag<1000,'no former 1500ms stability wait');
    await set(doc,'const barn = { hay: 1 ');await until(()=>api.getState().counters.strikes===5,'missing brace');
    assert.ok(api.getState().files.find(file=>file.uri===doc.uri.toString()).errors.some(error=>error.comment.includes('少了“}”')));
    await set(doc,'const barn = { hay: 1 };');await until(()=>api.getState().files.find(file=>file.uri===doc.uri.toString()).errors.length===0,'brace repair');assert.deepEqual(api.getState().counters,{strikes:5});
    const json=await vscode.workspace.openTextDocument({language:'json',content:''});await vscode.window.showTextDocument(json);await set(json,'{"hay": }');
    await until(()=>api.getState().counters.strikes===6,'real JSON red diagnostic beyond previous language list');await set(json,'{"hay": 1}');await until(()=>api.getState().files.find(file=>file.uri===json.uri.toString()).errors.length===0,'JSON repair');assert.deepEqual(api.getState().counters,{strikes:6});
    const cs=await vscode.workspace.openTextDocument({language:'csharp',content:''});await vscode.window.showTextDocument(cs);const collection=vscode.languages.createDiagnosticCollection('direct-fixture');
    try{
      const make=(severity,line=0)=>{const d=new vscode.Diagnostic(new vscode.Range(line,0,line,1),'Cannot implicitly convert string to int',severity);d.source='csharp';d.code='CS0029';return d;};
      collection.set(cs.uri,[make(vscode.DiagnosticSeverity.Warning)]);await sleep(150);assert.equal(api.getState().counters.strikes,6,'yellow warnings ignored');
      collection.set(cs.uri,[make(vscode.DiagnosticSeverity.Error)]);await until(()=>api.getState().counters.strikes===7,'C# red fixture accepted');
      collection.set(cs.uri,[make(vscode.DiagnosticSeverity.Error)]);await sleep(150);assert.equal(api.getState().counters.strikes,7,'identical publication does not hit again');
      collection.set(cs.uri,[]);await until(()=>api.getState().files.find(file=>file.uri===cs.uri.toString()).errors.length===0,'fixture repair');assert.deepEqual(api.getState().counters,{strikes:7});
      await vscode.commands.executeCommand('codeWhip.pause');collection.set(cs.uri,[make(vscode.DiagnosticSeverity.Error)]);await sleep(150);assert.equal(api.getState().counters.strikes,7);await vscode.commands.executeCommand('codeWhip.pause');
      collection.set(cs.uri,[]);await until(()=>api.getState().files.find(file=>file.uri===cs.uri.toString()).errors.length===0,'fixed after resume');assert.deepEqual(api.getState().counters,{strikes:7});
    }finally{collection.dispose();}
    await vscode.commands.executeCommand('codeWhip.settings');await vscode.commands.executeCommand('codeWhip.preview');assert.deepEqual(api.getState().counters,{strikes:8});
    const manifest=require('../package.json');assert.ok(!manifest.contributes.configuration.properties['codeWhip.scope']);assert.ok(!manifest.contributes.configuration.properties['codeWhip.tone']);
    fs.writeFileSync(result,JSON.stringify({passed:true,version:manifest.version,verifiedAt:new Date().toISOString(),realTypeScriptAndJsonDiagnostics:true,immediateDiagnosticDispatchLagMs:maxLag,moreThanThreeTriggersInOneFile:true,repeatedRepairAndMistake:true,repairSilent:true,yellowWarningsIgnored:true,csharpFixture:true,positionAndReasonComments:true,noSourceChanges:true,untitled:true,pause:true,compactSettings:true}));
  }catch(error){fs.writeFileSync(result,JSON.stringify({passed:false,error:error.stack}));throw error;}
}
module.exports={run};
