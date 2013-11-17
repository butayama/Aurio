﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AudioAlign.Audio.Matching;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using AudioAlign.Audio.Streams;
using System.Globalization;

namespace AudioAlign.Audio.Project {
    public class Project {

        private readonly TrackList<AudioTrack> audioTrackList;
        private readonly List<Match> matches;

        public Project() {
            audioTrackList = new TrackList<AudioTrack>();
            matches = new List<Match>();
            MasterVolume = 1;
        }

        public TrackList<AudioTrack> AudioTracks {
            get { return audioTrackList; }
        }

        public List<Match> Matches {
            get { return matches; }
        }

        public float MasterVolume { get; set; }
        public FileInfo File { get; set; }

        public static void Save(Project project, FileInfo targetFile) {
            if (targetFile.Exists) {
                //targetFile.Delete();
            }
            Stream stream = targetFile.Create();

            XmlTextWriter xml = new XmlTextWriter(stream, null);
            xml.WriteStartElement("project");
            
            // project format version
            xml.WriteStartElement("format");
            xml.WriteValue(1);
            xml.WriteEndElement();

            // audio tracks
            xml.WriteStartElement("audiotracks");
            foreach (AudioTrack track in project.AudioTracks) {
                xml.WriteStartElement("track");

                xml.WriteStartAttribute("file");
                xml.WriteString(GetFullOrRelativeFileName(targetFile, track.FileInfo));
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("name");
                xml.WriteString(track.Name);
                xml.WriteEndAttribute();

                if (track.Color != Track.DEFAULT_COLOR) {
                    xml.WriteStartAttribute("color");
                    xml.WriteString(track.Color);
                    xml.WriteEndAttribute();
                }

                xml.WriteStartAttribute("length");
                xml.WriteString(track.Length.ToString());
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("offset");
                xml.WriteString(track.Offset.ToString());
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("mute");
                xml.WriteValue(track.Mute);
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("solo");
                xml.WriteValue(track.Solo);
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("volume");
                xml.WriteValue(track.Volume);
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("balance");
                xml.WriteValue(track.Balance);
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("invertedphase");
                xml.WriteValue(track.InvertedPhase);
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("locked");
                xml.WriteValue(track.Locked);
                xml.WriteEndAttribute();

                xml.WriteStartElement("timewarps");
                foreach (TimeWarp warp in track.TimeWarps) {
                    xml.WriteStartElement("timewarp");

                    xml.WriteStartAttribute("from");
                    xml.WriteValue(warp.From);
                    xml.WriteEndAttribute();

                    xml.WriteStartAttribute("to");
                    xml.WriteValue(warp.To);
                    xml.WriteEndAttribute();

                    xml.WriteEndElement();
                }
                xml.WriteEndElement();

                xml.WriteEndElement();
            }
            xml.WriteEndElement();

            // matches
            xml.WriteStartElement("matches");
            foreach (Match match in project.Matches) {
                xml.WriteStartElement("match");

                xml.WriteStartAttribute("track1");
                xml.WriteValue(project.AudioTracks.IndexOf(match.Track1));
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("track1time");
                xml.WriteString(match.Track1Time.ToString());
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("track2");
                xml.WriteValue(project.AudioTracks.IndexOf(match.Track2));
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("track2time");
                xml.WriteString(match.Track2Time.ToString());
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("similarity");
                xml.WriteValue(match.Similarity);
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("source");
                xml.WriteValue(match.Source);
                xml.WriteEndAttribute();

                xml.WriteEndElement();
            }
            xml.WriteEndElement();

            // global settings
            xml.WriteStartElement("mastervolume");
            xml.WriteValue(project.MasterVolume);
            xml.WriteEndElement();

            xml.WriteEndElement();

            xml.Flush();
            xml.Close();
            project.File = targetFile;
        }

        public static Project Load(FileInfo sourceFile) {
            Project project = new Project();
            Stream stream = sourceFile.OpenRead();
            XmlTextReader xml = new XmlTextReader(stream);

            xml.ReadStartElement("project");

            // project format version
            xml.ReadStartElement("format");
            int formatVersion = xml.ReadContentAsInt();
            if (formatVersion != 1) {
                throw new Exception("invalid project file format");
            }
            xml.ReadEndElement();

            // audio tracks
            if (xml.IsStartElement("audiotracks")) {
                bool empty = xml.IsEmptyElement;
                xml.ReadStartElement("audiotracks");
                if (!empty) {
                    while (xml.IsStartElement("track")) {
                        xml.MoveToAttribute("file");
                        string file = xml.Value;
                        AudioTrack track = new AudioTrack(GetFileInfo(sourceFile, file));

                        xml.MoveToAttribute("name");
                        track.Name = xml.Value;

                        string color = xml.GetAttribute("color");
                        if (color != null) {
                            track.Color = color;
                        }

                        xml.MoveToAttribute("length");
                        track.Length = TimeSpan.Parse(xml.Value);

                        xml.MoveToAttribute("offset");
                        track.Offset = TimeSpan.Parse(xml.Value);

                        xml.MoveToAttribute("mute");
                        track.Mute = xml.ReadContentAsBoolean();

                        xml.MoveToAttribute("solo");
                        track.Solo = xml.ReadContentAsBoolean();

                        xml.MoveToAttribute("volume");
                        track.Volume = xml.ReadContentAsFloat();

                        xml.MoveToAttribute("balance");
                        track.Balance = xml.ReadContentAsFloat();

                        xml.MoveToAttribute("invertedphase");
                        track.InvertedPhase = xml.ReadContentAsBoolean();

                        if (xml.MoveToAttribute("locked")) {
                            track.Locked = xml.ReadContentAsBoolean();
                        }

                        xml.ReadStartElement("track");
                        if (xml.IsStartElement("timewarps")) {
                            empty = xml.IsEmptyElement;
                            xml.ReadStartElement("timewarps");
                            if (!empty) {
                                while (xml.IsStartElement("timewarp")) {
                                    TimeWarp warp = new TimeWarp();

                                    xml.MoveToAttribute("from");
                                    warp.From = xml.ReadContentAsLong();

                                    xml.MoveToAttribute("to");
                                    warp.To = xml.ReadContentAsLong();

                                    xml.ReadStartElement();
                                    //xml.ReadEndElement(); // not necessary since timewarp is an empty element

                                    track.TimeWarps.Add(warp);
                                }
                                xml.ReadEndElement(); // timewarps
                            }
                        }

                        xml.ReadEndElement(); // track
                        project.AudioTracks.Add(track);
                    }
                    xml.ReadEndElement(); // audiotracks
                }
            }

            // matches
            if (xml.IsStartElement("matches")) {
                bool empty = xml.IsEmptyElement;
                xml.ReadStartElement("matches");
                if (!empty) {
                    while (xml.IsStartElement("match")) {
                        
                        Match match = new Match();

                        xml.MoveToAttribute("track1");
                        match.Track1 = project.AudioTracks[xml.ReadContentAsInt()];

                        xml.MoveToAttribute("track1time");
                        match.Track1Time = TimeSpan.Parse(xml.Value);

                        xml.MoveToAttribute("track2");
                        match.Track2 = project.AudioTracks[xml.ReadContentAsInt()];

                        xml.MoveToAttribute("track2time");
                        match.Track2Time = TimeSpan.Parse(xml.Value);

                        xml.MoveToAttribute("similarity");
                        match.Similarity = xml.ReadContentAsFloat();

                        if (xml.MoveToAttribute("source")) {
                            match.Source = xml.ReadContentAsString();
                        }

                        project.Matches.Add(match);
                        xml.ReadStartElement("match");
                    }
                    xml.ReadEndElement(); // matches
                }
            }

            // global settings
            xml.ReadStartElement("mastervolume");
            project.MasterVolume = xml.ReadContentAsFloat();
            xml.ReadEndElement();

            xml.ReadEndElement();

            xml.Close();
            project.File = sourceFile;
            return project;
        }

        /// <summary>
        /// Exports a project timeline to Sony Vegas' proprietary EDL text file format.
        /// </summary>
        public static void ExportEDL(TrackList<AudioTrack> tracks, FileInfo targetFile) {
            string[] videoExtensions = { ".m2ts", ".mp4", ".mpg", ".avi", ".webm" };

            string[] edlFields = {
                "ID", // 1, 2, 3, ...
                "Track", // 0, 1, 2, ..
                "StartTime", // #.#### (milliseconds floating point)
                "Length", // #.####
                "PlayRate", // 1.000000
                "Locked", // FALSE
                "Normalized", // FALSE
                "StretchMethod", // 0
                "Looped", // TRUE
                "OnRuler", // FALSE
                "MediaType", // AUDIO, VIDEO(?)
                "FileName", // "C:\file.wav"
                "Stream", // 0
                "StreamStart", // 0.0000
                "StreamLength", // #.####
                "FadeTimeIn", // 0.0000
                "FadeTimeOut", // 0.0000
                "SustainGain", // 1.000000
                "CurveIn", // 2
                "GainIn", // 0.000000
                "CurveOut", // -2
                "GainOut", // 0.000000
                "Layer", // 0
                "Color", // -1
                "CurveInR", // -2
                "CurveOutR", // 2
                "PlayPitch", // 0.000000
                "LockPitch", // FALSE
                "FirstChannel", // 0
                "Channels" // 0
            };

            // preprocess tracks
            // - skip unmatched/unaligned tracks
            // - try to find a matching video file replacement for the audio file
            Dictionary<Track, int> tracklist = new Dictionary<Track, int>();
            int audioTrackNumber = 0;
            int videoTrackNumber = 1;
            foreach (AudioTrack track in tracks) {
                if (track.Volume == 0.0f) {
                    // skip tracks whose volume is muted (alignment sets track volumes to zero if a track has no matches)
                    continue;
                }

                FileInfo videoFileInfo = null;
                foreach (FileInfo fileInfo in track.FileInfo.Directory.EnumerateFiles(track.FileInfo.Name.Replace(".wav", ".*"))) {
                    foreach (string videoExtension in videoExtensions) {
                        if (fileInfo.Extension.Equals(videoExtension)) {
                            videoFileInfo = new FileInfo(fileInfo.FullName.Replace(fileInfo.Extension, videoExtension));
                            break;
                        }
                    }
                    if (videoFileInfo != null) {
                        break;
                    }
                }

                if (videoFileInfo != null) {
                    VideoTrack videoTrack = new VideoTrack(videoFileInfo) {
                        Length = track.Length,
                        Offset = track.Offset
                    };
                    AudioTrack audioTrack = new AudioTrack(videoFileInfo, false) {
                        Length = track.Length,
                        Offset = track.Offset
                    };
                    tracklist.Add(videoTrack, videoTrackNumber++);
                    tracklist.Add(audioTrack, audioTrackNumber++);
                }
                else {
                    tracklist.Add(track, audioTrackNumber++);
                }
            }

            StringBuilder sb = new StringBuilder();
            CultureInfo ci = new CultureInfo("en-US");
            
            // write headers
            for (int i = 0; i < edlFields.Length; i++) {
                sb.AppendFormat("\"{0}\"", edlFields[i]);
                if (i < edlFields.Length - 1) {
                    sb.Append(";");
                }
            }
            sb.AppendLine();

            // write tracks
            int trackID = 1;
            foreach(Track track in tracklist.Keys) {
                sb.AppendFormat(ci, "{0}; ", trackID++);
                sb.AppendFormat(ci, "{0}; ", tracklist[track]);
                sb.AppendFormat(ci, "{0:0.0000}; ", track.Offset.TotalMilliseconds);
                sb.AppendFormat(ci, "{0:0.0000}; ", track.Length.TotalMilliseconds);
                sb.AppendFormat(ci, "{0:0.000000}; ", 1);
                sb.Append("FALSE; ");
                sb.Append("FALSE; ");
                sb.AppendFormat(ci, "{0}; ", 0);
                sb.Append("TRUE; ");
                sb.Append("FALSE; ");
                sb.AppendFormat("{0}; ", track.MediaType.ToString().ToUpperInvariant());
                sb.AppendFormat(ci, "\"{0}\"; ", GetFullOrRelativeFileName(targetFile, track.FileInfo));
                sb.AppendFormat(ci, "{0}; ", 0);
                sb.AppendFormat(ci, "{0:0.0000}; ", 0);
                sb.AppendFormat(ci, "{0:0.0000}; ", track.Length.TotalMilliseconds);
                sb.AppendFormat(ci, "{0:0.0000}; ", 0);
                sb.AppendFormat(ci, "{0:0.0000}; ", 0);
                sb.AppendFormat(ci, "{0:0.000000}; ", 1);
                sb.AppendFormat(ci, "{0}; ", track.MediaType == MediaType.Video ? 4 : 2);
                sb.AppendFormat(ci, "{0:0.000000}; ", 0);
                sb.AppendFormat(ci, "{0}; ", track.MediaType == MediaType.Video ? 4 : -2);
                sb.AppendFormat(ci, "{0:0.000000}; ", 0);
                sb.AppendFormat(ci, "{0}; ", 0);
                sb.AppendFormat(ci, "{0}; ", -1);
                sb.AppendFormat(ci, "{0}; ", track.MediaType == MediaType.Video ? 4 : -2);
                sb.AppendFormat(ci, "{0}; ", track.MediaType == MediaType.Video ? 4 : 2);
                sb.AppendFormat(ci, "{0:0.000000}; ", 0);
                sb.Append("FALSE; ");
                sb.AppendFormat(ci, "{0}; ", 0);
                sb.AppendFormat(ci, "{0}", 0);
                sb.AppendLine();
            }

            // write to file
            StreamWriter writer = new StreamWriter(targetFile.FullName, false, Encoding.Default);
            writer.Write(sb.ToString());
            writer.Flush();
            writer.Close();
        }

        public static void ExportSyncXML(Project project, FileInfo targetFile) {
            Stream stream = targetFile.Create();

            string timeFormat = "G";
            IFormatProvider numberFormat = new CultureInfo("en-US");

            XmlTextWriter xml = new XmlTextWriter(stream, null);
            xml.Formatting = Formatting.Indented;
            xml.WriteStartElement("sync");


            // audio tracks
            xml.WriteStartElement("recordings");
            foreach (AudioTrack track in project.AudioTracks) {
                if (track.Volume == 0.0f) {
                    // skip tracks whose volume is muted
                    continue;
                }

                xml.WriteStartElement("recording");

                xml.WriteStartAttribute("name");
                xml.WriteString(Path.GetFileNameWithoutExtension(track.FileInfo.Name));
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("offset");
                xml.WriteString(track.Offset.ToString(timeFormat, numberFormat));
                xml.WriteEndAttribute();

                xml.WriteEndElement();
            }
            xml.WriteEndElement();

            // matches
            xml.WriteStartElement("syncpoints");
            foreach (Match match in project.Matches) {
                xml.WriteStartElement("syncpoint");

                xml.WriteStartElement("recording1");

                xml.WriteStartAttribute("name");
                xml.WriteValue(Path.GetFileNameWithoutExtension(match.Track1.FileInfo.Name));
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("time");
                xml.WriteString(match.Track1Time.ToString(timeFormat, numberFormat));
                xml.WriteEndAttribute();

                xml.WriteEndElement();

                xml.WriteStartElement("recording2");

                xml.WriteStartAttribute("name");
                xml.WriteValue(Path.GetFileNameWithoutExtension(match.Track2.FileInfo.Name));
                xml.WriteEndAttribute();

                xml.WriteStartAttribute("time");
                xml.WriteString(match.Track2Time.ToString(timeFormat, numberFormat));
                xml.WriteEndAttribute();

                xml.WriteEndElement();

                xml.WriteEndElement();
            }
            xml.WriteEndElement();

            xml.WriteEndElement();

            xml.Flush();
            xml.Close();
        }

        private static string GetFullOrRelativeFileName(FileInfo referenceFile, FileInfo targetFile) {
            Uri uri1 = new Uri(targetFile.FullName);
            Uri uri2 = new Uri(referenceFile.DirectoryName + Path.DirectorySeparatorChar);
            return Uri.UnescapeDataString(uri2.MakeRelativeUri(uri1).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }

        private static FileInfo GetFileInfo(FileInfo referenceFile, string fullOrRelativeFileName) {
            try {
                // try it as relative file
                return new FileInfo(referenceFile.DirectoryName + Path.DirectorySeparatorChar + fullOrRelativeFileName);
            } catch (Exception) {}
            // if the relative try fails, try as absolute path - if it still fails, the file doesn't exist
            return new FileInfo(fullOrRelativeFileName);
        }
    }
}
