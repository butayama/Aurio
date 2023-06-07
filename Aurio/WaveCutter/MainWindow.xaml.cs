﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using Aurio.Streams;
using Aurio;
using System.Diagnostics;
using System.IO;

namespace WaveCutter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private BackgroundWorker bw;

        public MainWindow()
        {
            Parameters = new Parameters
            {
                MinLength = 180,
                MaxLength = 600,
                SourceFiles = new ObservableCollection<FileInfo>()
            };

            InitializeComponent();
        }

        public Parameters Parameters { get; set; }

        private void openFileButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".wav";
            dlg.Filter = "Wave files|*.wav";
            dlg.Multiselect = true;

            if (dlg.ShowDialog() == true)
            {
                dlg.FileNames.ToList().ForEach(f => Parameters.SourceFiles.Add(new FileInfo(f)));
            }
        }

        private void executeButton_Click(object sender, RoutedEventArgs e)
        {
            if (Parameters.SourceFiles.Count > 0)
            {
                bw = new BackgroundWorker();
                bw.WorkerReportsProgress = true;
                bw.WorkerSupportsCancellation = true;
                bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
                bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
                bw.DoWork += new DoWorkEventHandler(bw_DoWork);

                openFileButton.IsEnabled = false;
                cancelButton.IsEnabled = true;

                bw.RunWorkerAsync(Parameters);
            }
        }

        private void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            Parameters parameters = (Parameters)e.Argument;

            var streams = parameters.SourceFiles.Select(fi => AudioStreamFactory.FromFileInfo(fi));

            // load source stream
            var sourceStream = new ConcatenationStream(streams.ToArray());

            var firstFile = parameters.SourceFiles.First();
            string targetFileNamePrefix = firstFile.FullName.Remove(
                firstFile.FullName.Length - firstFile.Extension.Length
            );
            string targetFileNameSuffix = firstFile.Extension;

            int partCount = 0;
            Random random = new Random();
            CropStream cropStream = new CropStream(sourceStream, 0, 0);
            while (sourceStream.Position < sourceStream.Length)
            {
                partCount++;
                int length = random.Next(parameters.MinLength, parameters.MaxLength); // length in seconds of the current part to write
                long byteLength = TimeUtil.TimeSpanToBytes(
                    new TimeSpan(TimeUtil.SECS_TO_TICKS * length),
                    cropStream.Properties
                );

                Debug.WriteLine(
                    "writing part "
                        + partCount
                        + " ("
                        + length
                        + " secs = "
                        + byteLength
                        + " bytes)"
                );
                Debug.WriteLine(
                    "before: "
                        + cropStream.Begin
                        + " / "
                        + cropStream.End
                        + " / "
                        + cropStream.Position
                        + " / "
                        + sourceStream.Position
                );
                cropStream.Begin = cropStream.End;
                cropStream.End +=
                    sourceStream.Length - cropStream.Begin < byteLength
                        ? sourceStream.Length - cropStream.Begin
                        : byteLength;
                cropStream.Position = 0;
                Debug.WriteLine(
                    "after : "
                        + cropStream.Begin
                        + " / "
                        + cropStream.End
                        + " / "
                        + cropStream.Position
                        + " / "
                        + sourceStream.Position
                );
                AudioStreamFactory.WriteToFile(
                    cropStream,
                    String.Format(
                        "{0}.part{1:000}{2}",
                        targetFileNamePrefix,
                        partCount,
                        targetFileNameSuffix
                    )
                );

                if (worker.CancellationPending)
                {
                    e.Cancel = true;
                    Debug.WriteLine("canceled");
                    return;
                }
                worker.ReportProgress(
                    (int)((double)sourceStream.Position / sourceStream.Length * 100)
                );
            }
            Debug.WriteLine("finished");
        }

        private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            openFileButton.IsEnabled = true;
            cancelButton.IsEnabled = false;
            progressBar1.Value = 0;
            Parameters.SourceFiles.Clear();
        }

        private void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            bw.CancelAsync();
        }
    }
}
