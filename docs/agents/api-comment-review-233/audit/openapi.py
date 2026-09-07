import json,pathlib,subprocess,sys
ROOT=pathlib.Path(sys.argv[1]).resolve();p='packages/sdk/openapi/v1.json';old=json.loads(subprocess.check_output(['git','show','258f27eabbe6676598a79097ff99cae899dc0e81:'+p],cwd=ROOT));new=json.loads((ROOT/p).read_text());assert new['servers']==old['servers']==[{'url':'http://localhost:5001/'}];allowed=set()
def allow(node,path,fields=('description',)):
 if isinstance(node,dict):
  for field in fields:allowed.add(path+(field,))
def schema(node,path):
 if not isinstance(node,dict):return
 allow(node,path)
 for field in ['properties','patternProperties','$defs','definitions','dependentSchemas']:
  for name,value in node.get(field,{}).items():schema(value,path+(field,name))
 for field in ['items','additionalProperties','unevaluatedProperties','contains','not','if','then','else','propertyNames','unevaluatedItems']:
  if isinstance(node.get(field),dict):schema(node[field],path+(field,))
 for field in ['allOf','anyOf','oneOf','prefixItems']:
  for i,value in enumerate(node.get(field,[])):schema(value,path+(field,i))
def content(node,path):
 for media,v in node.get('content',{}).items():schema(v.get('schema'),path+('content',media,'schema'))
def parameter(node,path):allow(node,path);schema(node.get('schema'),path+('schema',));content(node,path)
def response(node,path):
 allow(node,path);content(node,path)
 for name,v in node.get('headers',{}).items():parameter(v,path+('headers',name))
def body(node,path):allow(node,path);content(node,path)
def operation(node,path):
 allow(node,path,('summary','description'))
 for i,v in enumerate(node.get('parameters',[])):parameter(v,path+('parameters',i))
 for name,v in node.get('responses',{}).items():response(v,path+('responses',name))
 if 'requestBody' in node:body(node['requestBody'],path+('requestBody',))
def locations(doc):
 allow(doc.get('info'),('info',))
 for i,v in enumerate(doc.get('tags',[])):allow(v,('tags',i))
 for name,v in doc.get('paths',{}).items():
  path=('paths',name);allow(v,path,('summary','description'))
  for i,param in enumerate(v.get('parameters',[])):parameter(param,path+('parameters',i))
  for method in ['get','put','post','delete','options','head','patch','trace']:
   if method in v:operation(v[method],path+(method,))
 components=doc.get('components',{})
 for group,visit in [('schemas',schema),('parameters',parameter),('responses',response),('requestBodies',body),('headers',parameter),('securitySchemes',allow)]:
  for name,v in components.get(group,{}).items():visit(v,('components',group,name))
locations(old);locations(new);diff=[]
def compare(a,b,path=()):
 if a==b:return
 if path in allowed:
  assert (a is None or isinstance(a,str)) and (b is None or isinstance(b,str)),path;diff.append(dict(path=list(path),before=a,after=b));return
 if isinstance(a,dict) and isinstance(b,dict):
  for k in a.keys()|b.keys():
   if k not in a or k not in b:
    assert path+(k,) in allowed,('added/deleted contract field',path+(k,));compare(a.get(k),b.get(k),path+(k,))
   else:compare(a[k],b[k],path+(k,))
 elif isinstance(a,list) and isinstance(b,list):
  assert len(a)==len(b),('array length',path)
  for i,(x,y) in enumerate(zip(a,b)):compare(x,y,path+(i,))
 else:raise AssertionError(('contract difference',path,a,b))
compare(old,new)
print('PASS:',len(diff),'changes confined to identified OpenAPI documentation fields; contract data and server URL unchanged.')
