﻿using MyShogi.Model.Common.Utility;
using System;
using System.Diagnostics;

namespace MyShogi.Model.Common.Process
{
    /// <summary>
    /// 子プロセスを生成して、リダイレクトされた標準入出力経由でやりとりするためのクラス
    /// ローカルの思考エンジンに接続するときに用いる。
    /// </summary>
    public class ProcessNegotiator
    {
        public ProcessNegotiator()
        {
        }

        /// <summary>
        /// 思考エンジンに接続する。
        /// </summary>
        public void Connect(ProcessNegotiatorData engineData)
        {
            Disconnect();

            lock (lockObject)
            {
                if (engineData == null)
                {
                    throw new ArgumentNullException("engineData");
                }

                var info = new ProcessStartInfo
                {
                    FileName = engineData.ExeFilePath,
                    WorkingDirectory = engineData.ExeWorkingDirectory,
                    Arguments = engineData.ExeArguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = false,
                };
                
                var process = new System.Diagnostics.Process
                {
                    StartInfo = info,
                };

                // 子プロセスがいないとき、ここで例外が発生する。
                process.Start();

                // EXE用のprocess
                exeProcess = process;

                // remote service
                remoteService = new RemoteService(
                    process.StandardOutput.BaseStream, // read stream
                    process.StandardInput.BaseStream,  // write stream
                    true);

                IsLowPriority = engineData.IsLowPriority;
            }
        }

        /// <summary>
        /// 接続している子プロセスから行を読み込む。
        /// 読み込む行がなければ、ブロッキングせずにすぐ戻る。
        /// </summary>
        public void Read()
        {
            if (remoteService != null)
                remoteService.Read();
        }

        /// <summary>
        /// 接続している子プロセスに行を流し込む。
        /// </summary>
        /// <param name="s"></param>
        public void Write(string s)
        {
            if (remoteService != null)
                remoteService.Write(s);
        }

        /// <summary>
        /// 接続している思考エンジンを切断する。
        /// 実行中であっても強制的に切断して大丈夫なはず…。(エンジン側がきちんと終了処理をするはず..)
        /// </summary>
        public void Disconnect()
        {
            lock (lockObject)
            {
                if (exeProcess != null)
                {
                    exeProcess.Close();
                    //exeProcess.Kill();
                    
                    // Close()してからKill()できない。
                    // "quit"を送っているし、pipeは切断されているし、無事終了してくれることを祈るばかりだ。

                    exeProcess = null;
                }
                if (remoteService != null)
                {
                    remoteService.Dispose();
                    remoteService = null;
                }
            }
        }

        public void Dispose()
        {
            Disconnect();
        }

        /// <summary>
        /// 子プロセスの標準出力から新しい行を受信したときのコールバック
        /// Connect()のあとにRead()までにセットしておくこと。
        /// </summary>
        public CommandRecieveHandler CommandReceived
        {
            get { return remoteService.CommandReceived; }
            set { remoteService.CommandReceived = value; }
        }

        /// <summary>
        /// 接続した実行ファイルの優先度を変更します。
        /// IsLowPriorityがtrueであれば通常より低い優先度にします。
        /// (GUIがもっさりするときなど用)
        /// </summary>
        public void UpdateProcessPriority()
        {
            if (exeProcess == null)
            {
                throw new InvalidOperationException("exeProcess");
            }

            try
            {
                var priority = (IsLowPriority ?
                    ProcessPriorityClass.BelowNormal :
                    ProcessPriorityClass.Normal);

                if (this.exeProcess.PriorityClass != priority)
                {
                    this.exeProcess.PriorityClass = priority;
                }
            }
            catch (Exception ex)
            {
                Log.Write(LogInfoType.SystemError,"エンジンの実行優先順位を下げることができませんでした。:" + ex.ToString());
            }
        }

        /// <summary>
        /// 実行ファイルの優先度を普通より下げる。
        /// UpdateProcessPriority()したときに反映される。
        /// デフォルト = false
        /// </summary>
        public bool IsLowPriority { get; set; }

        // --- 以下private members

        /// <summary>
        /// Connect()で接続したプロセス
        /// </summary>
        private System.Diagnostics.Process exeProcess;

        /// <summary>
        /// 入出力のリダイレクトをして、入力があったときにコールバックするためのヘルパー。
        /// </summary>
        private RemoteService remoteService;

        /// <summary>
        /// 排他用
        /// </summary>
        private object lockObject = new object();

    }
}
