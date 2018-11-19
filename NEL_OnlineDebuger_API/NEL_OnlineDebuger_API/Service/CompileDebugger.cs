using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Neo.Compiler;
using Neo.Compiler.MSIL;    
using System.IO;
namespace NEL_OnlineDebuger_API.Service
{
    public class CompileDebugger
    {
        private PortableExecutableReference ref1, ref2, ref3, ref4;
        private CSharpCompilationOptions op;
        public CompileDebugger()
        {
            ref1 = MetadataReference.CreateFromFile("needlib" + Path.DirectorySeparatorChar + "mscorlib.dll");
            ref2 = MetadataReference.CreateFromFile("needlib" + Path.DirectorySeparatorChar + "System.dll");
            ref3 = MetadataReference.CreateFromFile("needlib" + Path.DirectorySeparatorChar + "System.Numerics.dll");
            ref4 = MetadataReference.CreateFromFile("needlib" + Path.DirectorySeparatorChar + "Neo.SmartContract.Framework.dll");
            op = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            var buildpath = Path.GetFullPath("needlib");
            if (!string.IsNullOrEmpty(buildpath))
            {
                Directory.SetCurrentDirectory(buildpath);
                //mono.cecil need load dll from here.
            }
        }
        public bool compile(string filename, string filetext, out byte[] avmtext, out string abitext, out string maptext, out string hash)
        {
            var tree = CSharpSyntaxTree.ParseText(filetext);
            var comp = CSharpCompilation.Create("aaa.dll", new[] { tree },
               new[] { ref1, ref2, ref3, ref4 }, op);
            
            var fs = new MemoryStream();
            var fspdb = new MemoryStream();
            var result = comp.Emit(fs, fspdb);
            fs.Seek(0, SeekOrigin.Begin);
            fspdb.Seek(0, SeekOrigin.Begin);

            ILModule mod = new ILModule();
            mod.LoadModule(fs, fspdb);
            
            NeoModule am = null;
            byte[] bytes = null;
            string jsonstr = null;
            string mapInfo = null;

            ConvOption option = new ConvOption() { useNep8 = false };
            var conv = new ModuleConverter(new DefLogger());
            am = conv.Convert(mod, option);
            // *.avm
            bytes = am.Build();
            avmtext = bytes;

            // *.abi.json
            var outjson = vmtool.FuncExport.Export(am, bytes);
            jsonstr = outjson.ToString();
            abitext = jsonstr;
            hash = outjson["hash"].ToString();

            // *.map.json
            Neo.Compiler.MyJson.JsonNode_Array arr = new Neo.Compiler.MyJson.JsonNode_Array();
            foreach (var m in am.mapMethods)
            {
                Neo.Compiler.MyJson.JsonNode_Object item = new Neo.Compiler.MyJson.JsonNode_Object();
                arr.Add(item);
                item.SetDictValue("name", m.Value.displayName);
                item.SetDictValue("addr", m.Value.funcaddr.ToString("X04"));
                Neo.Compiler.MyJson.JsonNode_Array infos = new Neo.Compiler.MyJson.JsonNode_Array();
                item.SetDictValue("map", infos);
                foreach (var c in m.Value.body_Codes)
                {
                    if (c.Value.debugcode != null)
                    {
                        var debugcode = c.Value.debugcode.ToLower();
                        //if (debugcode.Contains(".cs"))
                        {
                            infos.AddArrayValue(c.Value.addr.ToString("X04") + "-" + c.Value.debugline.ToString());
                        }
                    }
                }
            }
            mapInfo = arr.ToString();
            maptext = mapInfo;
            
            try
            {
                fs.Dispose();
                if (fspdb != null)
                    fspdb.Dispose();
            }
            catch
            {

            }
            return true;
        }
    }
}
