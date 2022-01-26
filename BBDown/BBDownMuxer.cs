using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using static BBDown.BBDownEntity;
using static BBDown.BBDownUtil;
using static BBDown.BBDownSubUtil;
using static BBDown.BBDownLogger;
using System.IO;

namespace BBDown
{
    class BBDownMuxer
    {
        public static int RunExe(string app, string parms)
        {
            if (File.Exists(Path.Combine(Program.APP_DIR, $"{app}")))
                app = Path.Combine(Program.APP_DIR, $"{app}");
            if (File.Exists(Path.Combine(Program.APP_DIR, $"{app}.exe")))
                app = Path.Combine(Program.APP_DIR, $"{app}.exe");
            int code = 0;
            Process p = new Process();
            p.StartInfo.FileName = app;
            p.StartInfo.Arguments = parms;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = false;
            p.ErrorDataReceived += delegate (object sendProcess, DataReceivedEventArgs output) {
                if (!string.IsNullOrWhiteSpace(output.Data))
                    Log(output.Data);
            };
            p.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            p.Start();
            p.BeginErrorReadLine();
            p.WaitForExit();
            p.Close();
            p.Dispose();
            return code;
        }

        private static string EscapeString(string str)
        {
            return str.Replace("\"", "'");
        }

        public static int MuxByMp4box(string videoPath, string audioPath, string outPath, string desc, string comment, string title, string episodeId, string cover, string lang, List<Subtitle> subs)
        {
            
            StringBuilder inputArg = new StringBuilder();
            StringBuilder metaArg = new StringBuilder(" -itags");
            inputArg.Append(" -inter 500 -noprog");
            if (!string.IsNullOrEmpty(videoPath))
                inputArg.Append($" -add \"{videoPath}#trackID=1\"");
            if (!string.IsNullOrEmpty(audioPath))
                inputArg.Append($" -add \"{audioPath}:lang={lang}\"");

            metaArg.Append($" desc=\"{desc}\"");    //第一个tag
            if (!string.IsNullOrEmpty(comment))
                metaArg.Append($":comment=\"{comment}\"");
            if (!string.IsNullOrEmpty(cover))
                metaArg.Append($":cover=\"{cover}\"");
            if (!string.IsNullOrEmpty(episodeId))
                metaArg.Append($":album=\"{title}\":title=\"{episodeId}\"");
            else
                metaArg.Append($":title=\"{title}\"");
            

            if (subs != null)
            {
                for (int i = 0; i < subs.Count; i++)
                {
                    if (File.Exists(subs[i].path) && File.ReadAllText(subs[i].path) != "")
                    {
                        inputArg.Append($" -add \"{subs[i].path}#trackID=1:name={GetSubtitleCode(subs[i].lan).Item2}:lang={GetSubtitleCode(subs[i].lan).Item1}\" ");
                    }
                }
            }

            //----分析完毕
            var arguments = inputArg.ToString() + metaArg.ToString() + $" -new \"{outPath}\"";
            LogDebug("mp4box命令：{0}", arguments);
            return RunExe("mp4box", arguments);
        }

        public static int MuxByFFmpeg(string videoPath, string audioPath, string outPath, string desc, string comment, string title, string episodeId, string cover, string lang, List<Subtitle> subs)
        {
            if (outPath.Contains("/") && !Directory.Exists(Path.GetDirectoryName(outPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            //----分析并生成-i参数
            var inputArg = new StringBuilder();
            var mapArg = new StringBuilder();
            var metaArg = new StringBuilder();
            int inputCnt = 0;

            if (!string.IsNullOrEmpty(videoPath))
            {
                inputArg.Append($" -i \"{videoPath}\"");
                mapArg.Append($" -map {inputCnt}:v");
                inputCnt++;
            }
            if (!string.IsNullOrEmpty(audioPath))
            {
                inputArg.Append($" -i \"{audioPath}\"");
                mapArg.Append($" -map {inputCnt}:a");
                inputCnt++;
                metaArg.Append($" -metadata:s:a:0 language=\"{lang}\"");
            }
            if (!string.IsNullOrEmpty(cover))
            {
                inputArg.Append($" -i \"{cover}\"");
                mapArg.Append($" -map {inputCnt}:v");
                inputCnt++;
                metaArg.Append(" -disposition:v:");
                if (string.IsNullOrEmpty(videoPath))
                    metaArg.Append("0");
                else
                    metaArg.Append("1");
                metaArg.Append(" attached_pic");
            }

            if (subs != null)
            {
                for (int i = 0; i < subs.Count; i++)
                {
                    if (File.Exists(subs[i].path) && File.ReadAllText(subs[i].path) != "")
                    {
                        inputArg.Append($" -i \"{subs[i].path}\"");
                        mapArg.Append($" -map {inputCnt}:s");
                        inputCnt++;
                        metaArg.Append($" -metadata:s:s:{i} handler_name=\"{GetSubtitleCode(subs[i].lan).Item2}\" -metadata:s:s:{i} language={GetSubtitleCode(subs[i].lan).Item1}");
                    }
                }
            }
            if (!string.IsNullOrEmpty(episodeId))
            {
                metaArg.Append($" -metadata title=\"{episodeId}\"");
                metaArg.Append($" -metadata album=\"{title}\"");
            }
            else
            {
                metaArg.Append($" -metadata title=\"{title}\"");
            }
            metaArg.Append($" -metadata description=\"{desc}\"");
            metaArg.Append($" -metadata comment=\"{comment}\"");
            metaArg.Append(" -c copy");
            if (subs != null) metaArg.Append(" -c:s mov_text");
            metaArg.Append(" -movflags faststart");
            //----分析完毕
            var arguments = $"-loglevel warning -y "
                 + inputArg.ToString()
                 + " " + metaArg.ToString()
                 + $" \"{outPath}\"";
            LogDebug("ffmpeg命令：{0}", arguments);
            return RunExe("ffmpeg", arguments);
        }
        public static int MuxAV(bool useMp4box, string videoPath, string audioPath, string outPath, string desc = "", string comment = "", string title = "", string episodeId = "", string pic = "", string lang = "", List<Subtitle> subs = null, bool audioOnly = false, bool videoOnly = false)
        {
            desc = EscapeString(desc);
            title = EscapeString(title);
            episodeId = EscapeString(episodeId);
            if (videoOnly) audioPath = "";
            if (audioOnly) videoPath = "";
            if (lang == "") lang = "und";

            if (useMp4box)
                return MuxByMp4box(videoPath, audioPath, outPath, desc, comment, title, episodeId, pic, lang, subs);
            else
                return MuxByFFmpeg(videoPath, audioPath, outPath, desc, comment, title, episodeId, pic, lang, subs);
        }

        public static void MergeFLV(string[] files, string outPath)
        {
            if (files.Length == 1)
            {
                File.Move(files[0], outPath); 
            }
            else
            {
                foreach (var file in files)
                {
                    var tmpFile = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".ts");
                    var arguments = $"-loglevel warning -y -i \"{file}\" -map 0 -c copy -f mpegts -bsf:v h264_mp4toannexb \"{tmpFile}\"";
                    LogDebug("ffmpeg命令：{0}", arguments);
                    RunExe("ffmpeg", arguments);
                    File.Delete(file);
                }
                var f = GetFiles(Path.GetDirectoryName(files[0]), ".ts");
                CombineMultipleFilesIntoSingleFile(f, outPath);
                foreach (var s in f) File.Delete(s);
            }
        }
    }
}
