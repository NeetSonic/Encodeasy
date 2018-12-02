using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Encodeasy.Model;
using Neetsonic.Tool;

namespace Encodeasy.View
{
    public partial class FrmMain : Form
    {
        private const string A_INPUT = @"##a_input##";
        private const string V_INPUT = @"##v_input##";
        private const string INPUT = @"##input##";
        private const string OUTPUT = @"##output##";
        private const string PRESET = @"##preset##";
        private const string CMD_ExtractMp4_Video = @"ffmpeg -i ""##input##"" -vcodec copy -an ""##output##""";
        private const string CMD_ExtractMp4_Audio = @"ffmpeg -i ""##input##"" -acodec copy -vn ""##output##""";
        private const string CMD_HandBrake = @"HandBrake.exe --preset-import-file ""##preset##"" --queue-import-file ""##input##""";
        private const string CMD_MergeMKV = @"mkvmerge -o ""##output##"" ""##v_input##"" ""##a_input##""";
        private const string CMD_VapourSynthAndX265 = @"vspipe.exe ""##input##"" --y4m - | x265-10bit_asuna --y4m -D 10 --preset slower --tune lp++ --ctu 32 --crf 18 --pbratio 1.2 --no-sao --me 3 --subme 4 --merange 44 --limit-tu 4 --b-intra --no-rect --no-amp --ref 4 --weightb --keyint 360 --min-keyint 1 --bframes 6 --aq-mode 3 --aq-strength 0.7 --rd 4 --psy-rd 1.5 --psy-rdoq 1.2 --rdoq-level 2 --no-open-gop --rc-lookahead 80 --scenecut 40 --qcomp 0.65 --no-strong-intra-smoothing --rskip --output ""##output##""";
        public FrmMain()
        {
            InitializeComponent();
        }
        private void BtnOpen_Click(object sender, EventArgs e)
        {
            string root = Application.StartupPath;
            string tempDir = Path.Combine(root, @"Temp");
            string templateDir = Path.Combine(root, @"Template");
            string outputDir = Path.Combine(root, @"Output");
            string handBreakeTemplate = Path.Combine(templateDir, @"job.json");
            string vapourSynthTemplate = Path.Combine(templateDir, @"waifu2x.vpy");
            string handBreakeParams = Path.Combine(templateDir, @"HandBrakeParams.json");
            string workBat = Path.Combine(root, @"work.bat");
            List<string> tempFiles = new List<string>{ workBat };
            List<MediaFile> files = new List<MediaFile>();
            StringBuilder build = new StringBuilder();

            OpenFileDialog dlg = new OpenFileDialog{Multiselect = true, Filter = @"MP4文件|*.mp4" };
            if(DialogResult.OK == dlg.ShowDialog())
            {
                files.AddRange(dlg.FileNames.Select(file => new MediaFile {Path = file}));
                int idx = 0;
                foreach(MediaFile file in files)
                {
                    // 编号
                    file.JobIndex = idx;

                    // 源
                    string srcFile = Path.Combine(tempDir, string.Format($@"src{idx}.mp4"));
                    tempFiles.Add(srcFile);
                    File.Copy(file.Path, srcFile);

                    // 抽取
                    string srcV = Path.Combine(tempDir, string.Format($@"V{idx}.mp4"));
                    string srcA = Path.Combine(tempDir, string.Format($@"V{idx}.aac"));
                    tempFiles.Add(srcV);
                    tempFiles.Add(srcA);
                    build.AppendLine(CMD_ExtractMp4_Video.Replace(INPUT, srcFile).Replace(OUTPUT, srcV));
                    build.AppendLine(CMD_ExtractMp4_Audio.Replace(INPUT, srcFile).Replace(OUTPUT, srcA));

                    // HandBrake配置文件生成
                    UTF8Encoding utf8NoBOM = new UTF8Encoding(false); // 微软默认含有BOM头，要去掉否则HandBreak不能识别
                    string fixedV = Path.Combine(tempDir, string.Format($@"V{idx}_fixed.mp4"));
                    string handBreakeFile = Path.Combine(tempDir, string.Format($@"job{idx}.json"));
                    tempFiles.Add(fixedV);
                    tempFiles.Add(handBreakeFile);
                    string handBreakeScript = FileTool.OpenAndReadAllText(handBreakeTemplate, utf8NoBOM).Replace(INPUT, srcV).Replace(OUTPUT, fixedV).Replace(@"\", @"\\");
                    FileTool.CreateAndWriteText(handBreakeFile, handBreakeScript, utf8NoBOM);

                    // 执行HandBrake
                    build.AppendLine(CMD_HandBrake.Replace(PRESET, handBreakeParams).Replace(INPUT, handBreakeFile));

                    // VapourSynth脚本生成
                    string vpyFile = Path.Combine(tempDir, string.Format($@"vpy{idx}.vpy"));
                    string waifu2xV = Path.Combine(tempDir, string.Format($@"V{idx}_waifu2x.hevc"));
                    tempFiles.Add(vpyFile);
                    tempFiles.Add(waifu2xV);
                    string vpyScript = FileTool.OpenAndReadAllText(vapourSynthTemplate, Encoding.UTF8).Replace(INPUT, fixedV);
                    FileTool.CreateAndWriteText(vpyFile, vpyScript, Encoding.UTF8);

                    // 执行VapourSynth和x265
                    build.AppendLine(CMD_VapourSynthAndX265.Replace(INPUT, vpyFile).Replace(OUTPUT, waifu2xV));

                    // 封装MKV
                    string output = Path.Combine(outputDir, string.Format($@"out{idx}.mkv"));
                    build.AppendLine(CMD_MergeMKV.Replace(OUTPUT, output).Replace(V_INPUT, waifu2xV).Replace(A_INPUT, srcA));

                    idx++;
                }

                // 生成BAT文件
                FileTool.CreateAndWriteText(workBat,build.ToString(), Encoding.Default);

                // 等待执行并删除临时文件

                // 重命名输出文件
            }
        }
    }
}