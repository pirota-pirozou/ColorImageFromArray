using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace ColorImageFromArray
{
    public partial class Form1 : Form
    {
        private int width = 256;
        private int height = 256;
        private int[] pixelData;
        private Bitmap? bitmap;
        private CachedBitmap? cachedBitmap;

        private Stopwatch? stopwatch;

        // マルチスレッド駆動
        private CancellationTokenSource? cts;
        private Task? task;

        private bool isRunning = false;

        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            pixelData = new int[width * height];
        }

        /// <summary>
        /// フォームがロードされたとき
        /// </summary>
        private void Form1_Load(object sender, EventArgs e)
        {
            // 数値配列にRGBデータを設定（例: カラーグラデーション）
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int r = 255 * x / width;
                    int g = 255 * y / height;
                    int b = 255 * (x + y) / (width + height);
                    pixelData[y * width + x] = (255 << 24) | (r << 16) | (g << 8) | b;
                }
            }

            // ビットマップを作成し、数値配列をコピー
            bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            // Stopwatchクラス生成
            stopwatch = new Stopwatch();
            stopwatch.Start();

            // タイマー設定
            SetupTimer();
        }

        /// <summary>
        /// 非同期でフレーム更新タイマーを設定
        /// </summary>
        private void SetupTimer()
        {
            cts = new CancellationTokenSource();

            isRunning = true;
            // タスク生成
            task = Task.Run(async () =>
            {
                while (isRunning)
                {
                    // タスクがキャンセルされていないかチェック
                    if (cts.Token.IsCancellationRequested)
                    {
                        Debug.WriteLine("Task is cancelled.");
                        break;
                    }
                    await Task.Delay(0);
                    // ここに 17ms ごとに実行したいコードを記述します
                    var ts = stopwatch?.Elapsed;
                    if (ts?.Milliseconds >= 17)
                    {
                        Debug.WriteLine("RunningLoop " + ts?.Milliseconds);
                        stopwatch?.Restart();
                        UpdateFrame(); // フレーム更新
                        this.Invalidate(); // 再描画
                    }
                }
            }, cts.Token);
        }

        /// <summary>
        /// 表示
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (!isRunning) return;

            // ビットマップを表示
            if (cachedBitmap != null)
            {
                g.DrawCachedBitmap(cachedBitmap, 0, 0);
            }
        }

        /// <summary>
        /// フレーム更新処理
        /// </summary>
        private void UpdateFrame()
        {
            if (bitmap == null) return;

            // 数値配列にRGBデータを設定（例: カラーグラデーション）
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData? bmpData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(pixelData, 0, bmpData.Scan0, pixelData.Length);
            bitmap.UnlockBits(bmpData);
            // CachedBitmapを更新
            using (Graphics g = this.CreateGraphics())
            {
                cachedBitmap = new CachedBitmap(bitmap, g);
            }
            bmpData = null;
            GC.Collect();
        }

        /// <summary>
        /// フォームを閉じる
        /// </summary>
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            //
            cts?.Cancel();
            isRunning = false;

            // タスクが終了するまで待機
            task?.Wait();
            task?.Dispose();
            task = null;
            stopwatch?.Stop();
            stopwatch = null;

            // キャンセルトークンを解放
            cts?.Dispose();
            cts = null;

            // アイドル状態イベント解除
            cts?.Dispose();
            cts = null;

            // cachedBitmap解放
            cachedBitmap?.Dispose();
            cachedBitmap = null;

            // bitmap解放
            bitmap?.Dispose();
            bitmap = null;

            GC.Collect();
        }

    }
}
