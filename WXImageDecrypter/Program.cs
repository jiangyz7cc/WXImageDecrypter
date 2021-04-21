//#define PRINT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using static System.Console;

namespace WXImageDecrypter
{
	enum IMG_TYPE
	{
		Unknow=0,
		JPG=1,
		PNG=2,
		GIF=3
	}

	class Program
	{
		static readonly byte[] JpgHead = new byte[] { 0xFF, 0xD8 };//, 0xFF };
		static readonly byte[] PngHead = new byte[] { 0x89, 0x50 };//, 0x4E };
		static readonly byte[] GifHead = new byte[] { 0x47, 0x49 };//, 0x46 };

		static List<string> lerrf = null;

		static void Main()
		{
			Write("--微信图片解密--\n");
				//+"(created:2020-4-16 update:2021-4-21)\n");
			Input();
		}

		private static void Input()
		{
			PrintHelp();

			string fin, fou;
			int ich;

			while ((ich = Read()) != 113) //ascii(q)=113
			{
				//Read: \r\n
				while (true)
				{
					if (Read() == 13) {
						Read();
						break;
					}
					else
						continue;
				}

				if (ich == 49) //ascii(1)=49
				{
					WriteLine("输入文件:");
					fin = ReadLine();

					if (!File.Exists(fin))
					{
						WriteLine("文件不存在！");
						WriteLine("输入文件:");
						fin = ReadLine();
					}

					WriteLine("输出文件:");
					fou = ReadLine();

#if DEBUG
					Stopwatch sw = new Stopwatch();
					sw.Start();
#endif

					DecryptFile(fin, fou);

#if DEBUG
					sw.Stop();

					WriteLine($"处理完成，耗时: {sw.ElapsedMilliseconds}ms");
#endif
				}
				else if (ich == 50) //ascii(2)=50
				{
					string dirinpth, diroupth;

					WriteLine("输入目录：");
					dirinpth = ReadLine();

					if (!Directory.Exists(dirinpth))
					{
						WriteLine("目录不存在！");
						WriteLine("输入目录：");
						dirinpth = ReadLine();
					}

					WriteLine("输出目录：");
					diroupth = ReadLine();

					if (!Directory.Exists(diroupth))
					{
						Directory.CreateDirectory(diroupth);
					}

					string[] files = Directory.GetFiles(dirinpth, "*.dat");

#if DEBUG
					Stopwatch sw = new Stopwatch();
					sw.Start();
#endif

					foreach (string f in files)
					{
						fou = diroupth + (diroupth[^1] == '\\' ? "" : "\\") + f.Substring(f.LastIndexOf("\\") + 1);
						DecryptFile(f, fou);
					}
#if DEBUG
					sw.Stop();
					WriteLine($"time elapsed: {sw.ElapsedMilliseconds}ms");
#endif
				}

				//输出错误文件：
				if (lerrf?.Count > 0)
				{
					WriteLine("无法确定类型的文件：");
					foreach (string f in lerrf)
					{
						WriteLine(f);
					}
				}

				PrintHelp();

			}

		}

		private static void PrintHelp()
		{
			Write("\n[1] 单文件\n"
			+ "[2] 目录\n"
			+ "[q] 退出"
			+ "\n");
		}


		/*
		JPEG(jpg): FFD8FFE0 | FFD8FFE1 | FFD8FFE8
		png: 89 50 4E 47 0D

		encryption method:
		key ^ byte -> new byte

		So we need to find the key,
		and the key can be calc through hit JPG, PNG formats' first byte.

		key ^ first byte -> FF | 89

		jpg:
		fb0 ^ FF -> keyjpg (keyjpg ^ FF = fb0)
		key <-- FF ^ byte --> fb0
		*/
		/// <summary>
		/// 解密文件
		/// </summary>
		/// <param name="fin">输入文件</param>
		/// <param name="fou">输出文件</param>
		private static void DecryptFile(string fin,string fou)
		{
			IMG_TYPE imgtype;
			imgtype = IMG_TYPE.Unknow;

#if PRINT
			WriteLine($"开始对{fin}解密...");
#endif

			using (FileStream fsin = new FileStream(fin, FileMode.Open))
			{
				using (FileStream fsou = new FileStream(fou, FileMode.Create))
				{
					byte key;
					byte b,bn;
					byte[] bh;
					bool gotkey;

					bh = new byte[2];
					gotkey = false;

					//获取文件前几个字节
					bh[0] = (byte)fsin.ReadByte();
					bh[1] = (byte)fsin.ReadByte();

					#region JPG
					//用文件的首个字节与JPG文件头首个字节异或，得到钥匙（假设的）
					key = (byte)(bh[0] ^ JpgHead[0]);

					//用得到的钥匙异或该字节
					bn = (byte)(key ^ bh[1]);

					//看得到的新字节是否等于JPG文件头的[1]字节
					//FF D8 FF
					if (bn == JpgHead[1])
					{
						gotkey = true;
						fsou.WriteByte(JpgHead[0]);
						fsou.WriteByte(bn);
						imgtype = IMG_TYPE.JPG;
					}
					#endregion

					if (!gotkey)
					{
						#region PNG
						key = (byte)(bh[0] ^ PngHead[0]);
						bn = (byte)(key ^ bh[1]);

						//把异或后得到的字节与PNG文件头[1]字节对比
						//89 50 4E
						if (bn == PngHead[1])
						{
							gotkey = true;
							fsou.WriteByte(PngHead[0]);
							fsou.WriteByte(bn);
							imgtype = IMG_TYPE.PNG;
						}
						#endregion
					}
					if (!gotkey)
					{
						key = (byte)(bh[0] ^ GifHead[0]);
						bn = (byte)(key ^ bh[1]);

						if (bn == GifHead[1])
						{
							gotkey = true;
							for (int i = 0; i < GifHead.Length; i++)
							{
								fsou.WriteByte(GifHead[i]);
							}
							imgtype = IMG_TYPE.GIF;
						}
					}

					if (!gotkey)
					{
						WriteLine($"错误：无法确定文件{fin}的类型！");
						if (lerrf == null)
							lerrf = new List<string>();
						lerrf.Add(fin);
						return;
					}

					int ib;
					while ((ib = fsin.ReadByte()) != -1)
					{
						b = (byte)ib;
						b ^= key;
						fsou.WriteByte(b);
					}

				}

			}

			string founew="";
			//根据图片类型转换文件名
			switch (imgtype)
			{
				case IMG_TYPE.JPG:
					founew= fou + ".jpg";
					File.Move(fou, founew);
					break;
				case IMG_TYPE.PNG:
					founew = fou + ".png";
					File.Move(fou, founew);
					break;
				default:
					break;
			}

#if PRINT
			WriteLine($"{fin}解密完成，输出文件位于：{founew}");
#endif

		}

	}
}
