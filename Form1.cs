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
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, ref Rectangle lpRect);
        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, ref Rectangle lpRect);

        private int width = 640;
        private int height = 480;
        private int[] pixelData;
        private Bitmap? bitmap;
#if USE_CACHEDBITMAP
        private CachedBitmap? cachedBitmap;
#endif
        private Stopwatch? stopwatch;

        // マルチスレッド駆動
        private CancellationTokenSource? cts;
        private Task? task;

        private bool isRunning = false;

        private Random random = new Random();

        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            pixelData = new int[width * height];
        }

        private Size CalculateFormSize(Size clientSize)
        {
            // 一時的なフォームを作成して枠のサイズを取得する
            using (Form tempForm = new Form())
            {
                tempForm.FormBorderStyle = this.FormBorderStyle;
                tempForm.WindowState = this.WindowState;

                // 現在の枠のサイズを取得する
                Size borderSize = new Size(
                    tempForm.Width - tempForm.ClientSize.Width,
                    tempForm.Height - tempForm.ClientSize.Height
                );

                // クライアントサイズに枠のサイズを加える
                return new Size(
                    clientSize.Width + borderSize.Width,
                    clientSize.Height + borderSize.Height
                );
            }
        }

        /// <summary>
        /// フォームがロードされたとき
        /// </summary>
        private void Form1_Load(object sender, EventArgs e)
        {
            // ボーダーサイズを考慮してクライアントサイズを設定
            // クライアント領域のサイズを設定する（例：640x480）
            Size desiredClientSize = new Size(640, 480);

            // フォームの全体サイズを計算する
            Size formSize = CalculateFormSize(desiredClientSize);

            // フォームのサイズを設定する
            this.MaximumSize = this.MinimumSize = formSize;
            this.Size = formSize;

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
                        float delta_t = (float)ts?.Milliseconds / 17.0f;
                        Debug.WriteLine("RunningLoop " + ts?.Milliseconds + " delta_t " + delta_t);
                        stopwatch?.Restart();
                        UpdateFrame(delta_t); // フレーム更新
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
#if USE_CACHEDBITMAP
            if (cachedBitmap != null)
            {
                g.DrawCachedBitmap(cachedBitmap, 0, 0);
            }
#else
            if (bitmap != null)
            {
                g.DrawImage(bitmap, 0, 0);
            }
#endif
        }

        /// <summary>
        /// フレーム更新処理
        /// </summary>
        /// <param name="delta_t">Δｔ(1.0=通常）</param>
        private void UpdateFrame(float delta_t)
        {
            if (bitmap == null) return;

            // 数値配列にRGBデータを設定（ランダム）
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // pixelData[y * width + x] = (255 << 24) | (r << 16) | (g << 8) | b;
                    int pixel = random.Next();
                    pixelData[y * width + x] = (255 << 24) | pixel;
                }
            }
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData? bmpData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(pixelData, 0, bmpData.Scan0, pixelData.Length);
            bitmap.UnlockBits(bmpData);
#if USE_CACHEDBITMAP
            // CachedBitmapを更新
            using (Graphics g = this.CreateGraphics())
            {
                cachedBitmap = new CachedBitmap(bitmap, g);
            }
#endif
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

#if USE_CACHEDBITMAP
            // cachedBitmap解放
            cachedBitmap?.Dispose();
            cachedBitmap = null;
#endif
            // bitmap解放
            bitmap?.Dispose();
            bitmap = null;

            GC.Collect();
        }

    }
}
