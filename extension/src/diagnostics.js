'use strict';
const clean=value=>String(value??'').replace(/[\r\n\u2028\u2029\x00-\x1f\x7f]/g,' ').replace(/\s+/g,' ').trim();
function signature(error){return JSON.stringify([clean(error.source),String(error.code??''),clean(error.message)]);}
class DiagnosticLedger{
  constructor(){this.files=new Map();}
  update(uri,errors){
    const previous=this.files.get(uri)||[],counts=new Map(),seen=new Map(),added=[];
    for(const error of previous){const key=signature(error);counts.set(key,(counts.get(key)||0)+1);}
    for(const error of errors){const key=signature(error),n=(seen.get(key)||0)+1;seen.set(key,n);if(n>(counts.get(key)||0))added.push(error);}
    this.files.set(uri,errors);
    return {added,cleared:previous.length>0&&errors.length===0};
  }
  clear(uri){this.files.delete(uri);}
}
module.exports={DiagnosticLedger,clean,signature};
