'use strict';
const {clean}=require('./diagnostics');
const locales=require('../media/locales.json');
function format(template,values){return template.replace(/\{(\w+)\}/g,(_,key)=>clean(values[key]));}
function reason(error,language){
  const l=locales[language]||locales.en,message=clean(error.message),code=String(error.code??'');
  // These are local templates for diagnostic facts, not guesses about intent.
  const quoted=message.match(/['‘“]([(){}\[\];:,])['’”]/);
  if((/expected|应为|需要|缺少/i.test(message)||code==='1005')&&quoted)return format(l.reasons.missing,{token:quoted[1]});
  if(/^(2322|2345|2339|CS0029|CS1503|reportArgumentType|reportAssignmentType)$/.test(code))return format(l.reasons.type,{message});
  if(/^(2304|2552|CS0103|reportUndefinedVariable)$/.test(code))return format(l.reasons.undefined,{message});
  if(/^(1002|1160|1161)$/.test(code)||/unterminated|未终止|未闭合/i.test(message))return format(l.reasons.string,{message});
  if(/^(1128|1109)$/.test(code)&&/^[}\])]/.test(error.token||''))return format(l.reasons.unexpected,{token:error.token[0],message});
  return format(l.reasons.generic,{message});
}
function comment(error,language){const l=locales[language]||locales.en;return format(l.comment,{line:error.line+1,column:error.column+1,reason:reason(error,language)});}
function commentText(words,language){
  if(['html','xml','vue','svelte'].includes(language))return '<!-- '+clean(words)+' -->';
  if(['css','scss','less'].includes(language))return '/* '+clean(words)+' */';
  if(['python','ruby','shellscript','powershell','yaml','toml','perl','r'].includes(language))return '# '+clean(words);
  if(['sql','lua'].includes(language))return '-- '+clean(words);
  return '// '+clean(words);
}
module.exports={comment,commentText,reason};
