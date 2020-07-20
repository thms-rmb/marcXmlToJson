using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Serialization;
using CommandLine;

namespace marcXmlToJson
{
    public class Util
    {
        public const string MarcNs = "http://www.loc.gov/MARC21/slim";
    }

    [XmlRoot("collection", Namespace=Util.MarcNs)]
    public class Collection
    {
        [XmlElement("record", Namespace=Util.MarcNs)]
        public List<Record> Records;
    }

    [XmlRoot("record", Namespace=Util.MarcNs)]
    public class Record
    {
        [XmlElement("controlfield", Namespace=Util.MarcNs)]
        public List<Controlfield> Controlfields;

        [XmlElement("datafield", Namespace=Util.MarcNs)]
        public List<Datafield> Datafields;
    }

    [XmlRoot("controlfield", Namespace=Util.MarcNs)]
    public class Controlfield
    {
        [XmlAttribute("tag", Namespace=Util.MarcNs)]
        public string Tag;

        [XmlText]
        public string Value;
    }

    [XmlRoot("datafield", Namespace=Util.MarcNs)]
    public class Datafield
    {
        [XmlAttribute("tag", Namespace=Util.MarcNs)]
        public string Tag;

        [XmlAttribute("ind1", Namespace=Util.MarcNs)]
        public string Ind1;

        [XmlAttribute("ind2", Namespace=Util.MarcNs)]
        public string Ind2;

        [XmlElement("subfield", Namespace=Util.MarcNs)]
        public List<Subfield> Subfields;
    }

    [XmlRoot("subfield", Namespace=Util.MarcNs)]
    public class Subfield
    {
        [XmlAttribute("code", Namespace=Util.MarcNs)]
        public string Code;

        [XmlText]
        public string Value;
    }

    public class RecordConverter : JsonConverter<Record>
    {
        public override Record Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => null;

        public override void Write(Utf8JsonWriter writer, Record value, JsonSerializerOptions options) {
            writer.WriteStartObject();
            foreach (var leader in value.Controlfields.Where(c => c.Tag == "000")) {
                writer.WriteString("leader", leader.Value);
            }

            writer.WriteStartArray("fields");
            foreach (var c in value.Controlfields.Where(c => c.Tag != "000")) {
                writer.WriteStartObject();
                writer.WriteString(c.Tag, c.Value);
                writer.WriteEndObject();
            }
            foreach (var d in value.Datafields) {
                writer.WriteStartObject();
                writer.WriteStartObject(d.Tag);
                writer.WriteStartArray("subfields");
                foreach (var s in d.Subfields) {
                    writer.WriteStartObject();
                    writer.WriteString(s.Code, s.Value);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteString("ind1", d.Ind1);
                writer.WriteString("ind2", d.Ind2);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }

    class Program
    {
        IEnumerable<Record> GetRecords(XmlReader reader)
        {
            var serializer = new XmlSerializer(typeof(Record));

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name.EndsWith("record"))
                    {
                        var recordReader = reader.ReadSubtree();
                        yield return (Record) serializer.Deserialize(recordReader);
                    }
            }
        }

        void Process(bool indented)
        {
            using (var fs = File.OpenRead("records.xml"))
            {
                var reader = XmlReader.Create(fs);
                var recordConverter = new RecordConverter();
                var stdout = Console.OpenStandardOutput();
                using (var writer = new Utf8JsonWriter(stdout, new JsonWriterOptions() {Indented = indented}))
                {
                    writer.WriteStartArray();
                    foreach (var record in GetRecords(reader))
                    {
                        recordConverter.Write(writer, record, default(JsonSerializerOptions));
                        writer.Flush();
                    }
                    writer.WriteEndArray();
                }
            }
        }

        public class Options
        {
            [Option('i', "indented", Required = false, HelpText = "Indent result JSON.")]
            public bool Indented { get; set; }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o => {
                    var program = new Program();
                    program.Process(o.Indented);
                });
        }
    }
}
