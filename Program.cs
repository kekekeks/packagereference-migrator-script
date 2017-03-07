using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace PackageReferenceMigrator
{
    class Program
    {
        static IEnumerable<string> FindProjects(string dir)
        {
            foreach (var sdir in Directory.GetDirectories(dir))
            foreach (var sres in FindProjects(sdir))
                yield return sres;
            foreach (var f in Directory.GetFiles(dir, "*.csproj"))
                yield return f;
        }


        class PackageReference
        {
            public string Id { get; set; }
            public string Version { get; set; }
        }

        static XName GetName(string name)=>XName.Get(name, "http://schemas.microsoft.com/developer/msbuild/2003");

        static void Migrate(string file)
        {
            var dir = Path.GetDirectoryName(file);
            var pkgconfig = Path.Combine(dir, "packages.config");
            if (!File.Exists(pkgconfig))
                return;
            var packages = XDocument.Load(pkgconfig).Root.Elements("package").Select(p => new PackageReference
            {
                Id = p.Attribute("id").Value,
                Version = p.Attribute("version").Value
            }).ToList();

            var project = XDocument.Load(file, LoadOptions.PreserveWhitespace);
            project.Root.SetAttributeValue("ToolsVersion", "15.0");
            foreach (var reference in project.Root.Descendants(GetName("Reference")).ToList())
            {
                var hintPath = reference.Elements(GetName("HintPath")).FirstOrDefault()?.Value;
                if (hintPath != null && hintPath.Contains("\\packages\\"))
                {
                    var ws = reference.PreviousNode as XText;
                    reference.Remove();
                    ws?.Remove();
                }
            }

            var grp = new XElement(GetName("ItemGroup"));
            foreach (var pkg in packages)
            {
                grp.Add("\r\n    ");
                var reference = new XElement(GetName("PackageReference"));
                reference.SetAttributeValue("Include", pkg.Id);
                reference.Add("\r\n      ");
                reference.Add(new XElement(GetName("Version"), pkg.Version));
                reference.Add("\r\n    ");
                grp.Add(reference);
            }
            grp.Add("\r\n  ");

            project.Root.Add("  ");
            project.Root.Add(grp);
            project.Root.Add("\r\n");
            project.Save(file);
            File.Delete(pkgconfig);
        }

        static void Main(string[] args)
        {
            foreach (var project in FindProjects(Directory.GetCurrentDirectory()))
            {
                Migrate(project);
            }

        }
    }
}
