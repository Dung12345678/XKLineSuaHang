using BMS.Business;
using BMS.Model;
using BMS.Utils;
using DevExpress.Skins;
using Forms;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BMS
{
	public partial class frmXK : _Forms
	{
		int _row = -1;
		string PartCode = "";
		string _pathError = Path.Combine(Application.StartupPath, "Errors");
		int _focusbtnSave = 0;
		DataTable dtTPSon;
		DataTable _dtOrderPart;
		DataTable dtSon;
		List<int> _lstID = new List<int>();
		List<String> lst = new List<string>();
		List<string> _lstArticleID = new List<string>();
		ASCIIEncoding _encoding = new ASCIIEncoding();
		string _socketIPAddressPicking = "172.0.0.1";
		int _socketPortPicking = 2000;
		Socket _socketPicking;
		DateTime _startMakeTime;
		DateTime _endMakeTime;
		Thread _threadLoadAll;
		Thread _threadConnect;
		string _Casse = "";
		string _Motor = "";
		DataTable _dtStockCD;
		List<int> _lstCount = new List<int>();
		List<int> _lstRow = new List<int>();

		string contentError;
		Thread _threadShelfNumber;
		public frmXK()
		{
			InitializeComponent();
		}
		private void frmAssembleStock_Load(object sender, EventArgs e)
		{

			string version = File.ReadAllText(Application.StartupPath + "/Version.txt");
			if (File.Exists(Application.StartupPath + "/Line.txt"))
			{
				cboLine.SelectedIndex = TextUtils.ToInt(File.ReadAllText(Application.StartupPath + "/Line.txt"));
			}
			this.Text += "  -  Version: " + version.Trim();
			//DocUtils.InitFTPQLSX();
			//Check update version
			//updateVersion();
			//tableLayoutPanel1.SetColumnSpan(txtQtyAccessory, 3);
			//tableLayoutPanel1.SetColumnSpan(txtOrder, 3);
			tableLayoutPanel1.SetColumnSpan(txtDescriptionAssembleStock, 5);

			_startMakeTime = DateTime.Now;
			//ConnectAnDonPicking();
			loadCboStock();
			txtWorkerCode.Focus();
			NumSTT.Enabled = false;

			//_threadConnect = new Thread(new ThreadStart(ConnectAnDonPicking));
			//_threadConnect.IsBackground = true;
			//_threadConnect.Start();

			//sendDataTCPAnDonPicking("SD", "XK");

		}
		void updateVersion()
		{
			Lib.UpdateVersion();
		}
		/// <summary>
		/// Kết nối AnDon
		/// </summary>
		void ConnectAnDonPicking()
		{
			while (true)
			{
				Thread.Sleep(200);
				try
				{
					if (_socketPicking != null && _socketPicking.Connected)
					{
						if (!Lib.IsSocketConnected(_socketPicking))
						{
							_socketPicking = null;
						}
					}
					else
					{
						//Load ra config trong database lấy địa chỉ tcp, port
						DataTable dtConfig = TextUtils.Select("SELECT TOP 1 * FROM [ShiStock].[dbo].[AndonPickingConfig] with (nolock)");
						_socketIPAddressPicking = TextUtils.ToString(dtConfig.Rows[0]["TcpIp"]);
						_socketPortPicking = TextUtils.ToInt(dtConfig.Rows[0]["SocketPort"]);
						IPAddress ipAddOut = IPAddress.Parse(_socketIPAddressPicking);
						IPEndPoint endPoint = new IPEndPoint(ipAddOut, _socketPortPicking);
						_socketPicking = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
						_socketPicking.Connect(endPoint);
						_socketPicking.ReceiveTimeout = 100;
					}
				}
				catch (Exception ex)
				{
					//MessageBox.Show("Can't connect to Andon!");
					_socketPicking = null;
				}
			}
		}


		/// <summary>
		/// Gửi tín hiệu lên ANDON
		/// </summary>
		/// <param name="value">  </param>
		void sendDataTCPAnDonPicking(string value, string XK)
		{
			try
			{
				//Gửi tín hiệu delay xuống server Andon qua TCP/IP
				if (_socketPicking != null && _socketPicking.Connected)
				{
					this.Invoke((MethodInvoker)delegate
					{
						if (cboLine.SelectedIndex == 2) return;
						try
						{
							string sendData;
							sendData = string.Format("{0};{1};{2}", cboKho.Text.Trim(), value, XK);

							byte[] data = _encoding.GetBytes(sendData);
							_socketPicking.Send(data);

						}
						catch (Exception ex)
						{
							//ConnectAnDonPicking();
							ErrorLog.errorLog(value, "Truyền TCP lỗi", Environment.NewLine);
						}
					});
				}
				else
				{
					ErrorLog.errorLog(value, "K kết nối được với AnDon", Environment.NewLine);
				}
			}
			catch (Exception ex)
			{
				//Ghi log vào 
				_socketPicking = null;
				//MessageBox.Show(ex.ToString() + Environment.NewLine);
			}
		}
		/// <summary>
		/// Load kho
		/// </summary>
		void loadCboStock()
		{
			try
			{
				DataTable dt = TextUtils.Select("SELECT * FROM dbo.AssemblyStock WITH(NOLOCK)");
				DataRow r = dt.NewRow();
				r["ID"] = 0;
				r["Name"] = "";
				dt.Rows.InsertAt(r, 0);
				cboKho.DataSource = dt;
				cboKho.DisplayMember = "Code";
				cboKho.ValueMember = "ID";
			}
			catch (Exception)
			{
			}

		}


		private void loadStockCD()
		{
			//_dtStockCD = TextUtils.Select($"SELECT * FROM dbo.StockCD where StockID={TextUtils.ToInt(cboKho.SelectedValue)}");
			//DataRow r = _dtStockCD.NewRow();
			//r["ID"] = 0;
			//r["CDName"] = "";
			//_dtStockCD.Rows.InsertAt(r, 0);
			//cboStockCD.DataSource = _dtStockCD;
			//cboStockCD.DisplayMember = "CDName";
			//cboStockCD.ValueMember = "ID";
		}
		bool loadPlan(string order)
		{
			try
			{
				DataSet ds = TextUtils.LoadDataSetFromSP("spLoadProductPlan", new string[] { "@OrderCode" }, new object[] { order });
				if (ds.Tables.Count <= 0) return false;
				DataTable dtHyp = ds.Tables[0];
				DataTable dtAltax = ds.Tables[1];
				//Line Hyp
				if (cboLine.SelectedIndex == 1)
				{
					if (dtHyp.Rows.Count > 0)
					{
						txtQtyAssembleStock.Text = TextUtils.ToString(dtHyp.Rows[0]["Qty"]);
						txtTargetAssembleStock.Text = TextUtils.ToString(dtHyp.Rows[0]["ShipTo"]);
						txtPidAssembleStock.Text = TextUtils.ToString(dtHyp.Rows[0]["ProductCode"]);
						txtDescriptionAssembleStock.Text = TextUtils.ToString(dtHyp.Rows[0]["Description"]);
						NumSTT.Value = TextUtils.ToDecimal(dtHyp.Rows[0]["Stt"]);
						DtpDatePlan.Value = TextUtils.ToDate3(dtHyp.Rows[0]["JgDate"]);
					}
					else
					{
						return false;
					}
				}
				//Line Altax
				if (cboLine.SelectedIndex == 2)
				{
					if (dtAltax.Rows.Count > 0)
					{
						cboLine.SelectedIndex = 2;
						txtQtyAssembleStock.Text = TextUtils.ToString(dtAltax.Rows[0]["Qty"]);
						txtTargetAssembleStock.Text = TextUtils.ToString(dtAltax.Rows[0]["ShipTo"]);
						txtPidAssembleStock.Text = TextUtils.ToString(dtAltax.Rows[0]["ProductCode"]);
						txtDescriptionAssembleStock.Text = TextUtils.ToString(dtAltax.Rows[0]["Description"]);
						NumSTT.Value = TextUtils.ToDecimal(dtAltax.Rows[0]["Stt"]);
						DtpDatePlan.Value = TextUtils.ToDate3(dtAltax.Rows[0]["JgDate"]);
					}
					else
					{
						return false;
					}
				}
				setFocus();
				return true;
				//});
				//await task;
			}
			catch
			{
				return false;
			}
		}
		void checkPaint()
		{

			_lstArticleID = new List<string>();
			List<string> lstError = new List<string>();
			_lstID = new List<int>();

			DataRow[] rows = dtSon.Select($"Shelf like '99%'");
			if (rows.Length != 0)
			{
				//Check 9999
				for (int i = 0; i < rows.Length; i++)
				{
					DataRow r = rows[i];
					DataTable dt = TextUtils.Select($"SELECT top 1 * FROM dbo.PartSon where PartCode = '{TextUtils.ToString(r["ArticleID"])}'");
					if (dt.Rows.Count == 0) continue;
					_lstArticleID.Add(TextUtils.ToString(dt.Rows[0]["PartCode"]));
					int id = TextUtils.ToInt(dt.Rows[0]["ID"]);
					_lstID.Add(id);
					int qtyPaint = TextUtils.ToInt(dt.Rows[0]["QuantityAssembling"]);
					//int qtyOrder = TextUtils.ToInt(r["Qty"]);
					int qtyOrder = Lib.ToInt(txtQtyAssembleStock.Text.Trim());
					if (qtyOrder > qtyPaint)
					{
						lstError.Add("Mã Hàng: " + TextUtils.ToString(r["ArticleID"]) + " " + $"Yêu cầu: {qtyOrder}" + " " + $"Thực tế: {qtyPaint}");
					}
				}
			}
			//Check ArticleID có trong kho sơn không
			for (int i = 0; i < dtTPSon.Rows.Count; i++)
			{
				string ArticleID = TextUtils.ToString(dtTPSon.Rows[i]["ArticleID"]);
				Expression exp = new Expression("PartCode", ArticleID);
				ArrayList arr = PartSonBO.Instance.FindByExpression(exp);
				if (arr.Count > 0)
				{
					PartSonModel partSon = (PartSonModel)arr[0];
					if (partSon.ID > 0)
					{
						_lstArticleID.Add(partSon.PartCode);
						_lstID.Add(partSon.ID);
						if (TextUtils.ToInt(txtQtyAssembleStock.Text.Trim()) > partSon.QuantityAssembling)
						{
							lstError.Add("Mã Hàng: " + ArticleID + " " + $"Yêu cầu: {TextUtils.ToInt(txtQtyAssembleStock.Text.Trim())}" + " " + $"Thực tế: {partSon.QuantityAssembling}");
						}
					}
				}
			}
			if (lstError.Count > 0)
			{

				contentError = string.Join("\n", lstError);
				//MessageBox.Show(contentError, "RTC", MessageBoxButtons.OK, MessageBoxIcon.Stop);
				//WarningForm frmWarning = new WarningForm();
				//frmWarning.LBTieuDe = "KHÔNG ĐỦ LINH KIỆN XUẤT KHO";
				//frmWarning.LB = $"{contentError}";
				//frmWarning.ShowDialog();
			}
			//}));

			//});
			//await task;
		}
		private void txtOrder_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode != Keys.Enter) return;
			if (txtOrderr.Text.Trim() == "")
			{
				txtQtyAssembleStock.Text = "";
				txtTargetAssembleStock.Text = "";
				txtPidAssembleStock.Text = "";
				txtDescriptionAssembleStock.Text = "";

				grdData.DataSource = null;
				return;
			}
			if (!loadPlan(txtOrderr.Text.Trim()))
			{
				WarningForm frmWarning = new WarningForm();
				frmWarning.LB = "ORDER KHÔNG CÓ TRONG KẾ HOẠCH";
				frmWarning.NameKho = cboKho.Text.Trim();
				frmWarning.Order = txtOrderr.Text.Trim();
				frmWarning.ShowDialog();
				Reset();
				grdData.DataSource = null;
				return;
			}
			if (!loadOrderPart(txtOrderr.Text.Trim()))
			{
				WarningForm frmWarning = new WarningForm();
				frmWarning.LB = "KHÔNG TÌM THẤY ORDER TRONG LỊCH SỬ";
				frmWarning.NameKho = cboKho.Text.Trim();
				frmWarning.Order = txtOrderr.Text.Trim();
				frmWarning.ShowDialog();
				Reset();
				grdData.DataSource = null;
				//MessageBox.Show("Order không có vật tư!", "RTC", MessageBoxButtons.OK, MessageBoxIcon.Stop);
				return;
			}
			_lstCount.Clear();
			_startMakeTime = DateTime.Now;
			if (cboKho.Text.Trim().ToUpper().Contains("MOTOR"))
			{
				for (int k = 0; k < grvData.RowCount; k++)
				{
					string ArticleID = TextUtils.ToString(grvData.GetRowCellValue(k, colArticleID));
					DataTable dt = TextUtils.GetDataTableFromSP("spLoadHistoryShelf", new string[] { "@ArticleID" }, new object[] { ArticleID });
					if (dt.Rows.Count > 0)
					{
						//Hiển thị vị trí lên cột vị trí trong gridview
						grvData.SetRowCellValue(k, colShelf, TextUtils.ToString(dt.Rows[0][0]));
					}
				}
			}
			contentError = "";
			//if (cboKho.Text.Trim().ToUpper().Contains("KT") && cboLine.SelectedIndex == 1)
			//{
			//	checkPaint();
			//}
			//if (cboKho.Text.Trim().ToUpper().Contains("KT") && cboLine.SelectedIndex == 2)
			//{
			//	//check trong kho sơn giá trị nó lớn hơn so với 
			//	checkPaintLineAltax();
			//	//Gán giá trị lên
			//	//Save 
			//}
			if (_lstArticleID.Count > 0)
			{
				if (cboKho.Text.Trim().Contains("KT"))
				{
					string inform = "";
					frmArticleID frm = new frmArticleID();
					for (int i = 0; i < _lstArticleID.Count; i++)
					{
						inform += "Mã hàng:" + _lstArticleID[i] + "- Số lượng:" + txtQtyAssembleStock.Text.Trim() + "\n";
					}
					frm.ArticleID = inform;
					if (frm.ShowDialog() == DialogResult.OK)
					{
						List<SONHistoryImExModel> lstHistory = new List<SONHistoryImExModel>();
						// Lưu và trừ kho sơn nếu trong kho sơn có 
						for (int i = 0; i < _lstID.Count; i++)
						{
							// lấy ID theo từng dòng
							int ID = _lstID[i];
							string ArticleID = _lstArticleID[i];
							PartSonModel partSon = (PartSonModel)PartSonBO.Instance.FindByPK(ID);
							//	partSon.QuantityExporting;
							int RealQty = TextUtils.ToInt(txtQtyAssembleStock.Text.Trim());
							//Check trong lịch sử xuất kho sơn có không
							int check = TextUtils.ToInt(TextUtils.ExcuteScalar($"SELECT TOP 1 1 FROM [ShiStock].[dbo].[SONHistoryImEx] WHERE OrderCode=N'{txtOrderr.Text.Trim()}' AND PartCode=N'{partSon.PartCode}' and IsExported=1 and IsAssembled=1"));
							if (check == 1) continue;
							partSon.QuantityAssembling -= RealQty;
							if (partSon.QuantityAssembling < 0)
							{
								//MessageBox.Show($"Số lượng trong kho sơn không đủ thiếu {-partSon.QuantityAssembling}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
								WarningForm frmWarning = new WarningForm();
								frmWarning.NameKho = cboKho.Text.Trim();
								frmWarning.Order = txtOrderr.Text.Trim();
								frmWarning.LB = $"{contentError}";
								frmWarning.ShowDialog();
								return;
							}
							PartSonBO.Instance.Update(partSon);
							SONHistoryImExModel sONHistoryImEx = new SONHistoryImExModel();
							sONHistoryImEx.PartID = partSon.ID;
							sONHistoryImEx.PartCode = partSon.PartCode;
							sONHistoryImEx.DateImEx = DateTime.Now;
							sONHistoryImEx.Quantity = RealQty;
							sONHistoryImEx.OrderCode = txtOrderr.Text.Trim();
							sONHistoryImEx.ProductCode = txtPidAssembleStock.Text.Trim();
							sONHistoryImEx.IsExported = true; // 1 xuất 0 nhập
							sONHistoryImEx.IsAssembled = 1;// 1 lắp ráp 0 xuất khẩu
							sONHistoryImEx.WorkerCode = txtWorkerCode.Text.Trim();
							//SONHistoryImExBO.Instance.Insert(sONHistoryImEx);
							lstHistory.Add(sONHistoryImEx);
							//SONHistoryImExBO.Instance.Insert(sONHistoryImEx);
						}
						saveHistory(lstHistory);
						_lstID.Clear();
						_lstArticleID.Clear();
					}
					else
					{
						txtOrderr.Text = "";
						txtOrderr.Focus();
						grdData.DataSource = null;
						return;
					}

				}
			}
		}
		bool loadOrderPart(string order)
		{
			try
			{
				string StockCode = "";
				if (cboKho.Text.Trim().ToUpper().Contains("KT"))
				{
					StockCode = "KT%";
				}
				else if (cboKho.Text.Trim().ToUpper().Contains("KN"))
				{
					StockCode = "KN%";
				}
				else if (cboKho.Text.Trim().ToUpper().Contains("KCASSE"))
				{
					StockCode = "KCASSE%";
				}
				else if (cboKho.Text.Trim().ToUpper().Contains("KMOTOR"))
				{
					StockCode = "KMOTOR%";
				}
				DataSet ds = TextUtils.LoadDataSetFromSP("spGetPartsExported", new string[] { "@OrderCode", "@StockCode" }, new object[] { txtOrderr.Text.Trim(), StockCode });
				if (ds.Tables.Count < 0) return false;
				_dtOrderPart = ds.Tables[1];
				DataTable dtMaster = ds.Tables[0];
				if (dtMaster.Rows.Count <= 0) return false;
				txtStockCD.Text = TextUtils.ToString(dtMaster.Rows[0]["StockCDCode"]);
				//if (cboKho.Text.Trim().Contains("KT"))
				//{
				//	dtSon = ds.Tables[3];
				//	dtTPSon = ds.Tables[4];
				//}
				DataColumnCollection columns = _dtOrderPart.Columns;
				if (!columns.Contains("Accessory"))
				{
					DataColumn dtc = new DataColumn("Accessory");
					DataColumn dtc1 = new DataColumn("Lo");
					DataColumn dtc2 = new DataColumn("RealQty");
					_dtOrderPart.Columns.Add(dtc);
					_dtOrderPart.Columns.Add(dtc1);
					_dtOrderPart.Columns.Add(dtc2);

				}
				else
				{
					_focusbtnSave = 1;
				}

				if (_dtOrderPart.Rows.Count == 0)
				{
					return false;
				}
				else
				{
					this.Invoke(new MethodInvoker(delegate ()
					{
						//Check bảng _dtOrderPart có trùng mã hàng ArticleID (trùng thì cộng tổng số lượng với nhau)

						grdData.DataSource = _dtOrderPart;

						for (int i = 0; i < _dtOrderPart.Rows.Count; i++)
						{
							int j = TextUtils.ToInt(grvData.GetRowCellValue(i, colQty));

							if (cboKho.Text.Trim() == "KMotor" || cboKho.Text.Trim() == "KCasse" || cboKho.Text.Trim().Contains("KN"))
							{
								break;
							}
							grvData.SetRowCellValue(i, "RealQty", j);
						}
						setFocus();
					}));
				}
				return true;
			}
			catch
			{
				return false;
			}
		}
		void checkPaintLineAltax()
		{
			_lstArticleID = new List<string>();
			List<string> lstError = new List<string>();
			_lstID = new List<int>();
			//Check ArticleID có trong kho sơn không
			for (int i = 0; i < grvData.RowCount; i++)
			{
				string ArticleID = TextUtils.ToString(grvData.GetRowCellValue(i, colArticleID));
				Expression exp = new Expression("PartCode", ArticleID);
				ArrayList arr = PartSonBO.Instance.FindByExpression(exp);
				if (arr.Count > 0)
				{
					PartSonModel partSon = (PartSonModel)arr[0];
					if (partSon.ID > 0)
					{
						_lstArticleID.Add(partSon.PartCode);
						_lstID.Add(partSon.ID);
						if (TextUtils.ToInt(txtQtyAssembleStock.Text.Trim()) > partSon.QuantityAssembling)
						{
							lstError.Add("Mã Hàng: " + ArticleID + " " + $"Yêu cầu: {TextUtils.ToInt(txtQtyAssembleStock.Text.Trim())}" + " " + $"Thực tế: {partSon.QuantityAssembling}");
						}
						else
						{
							//Gán giá trị lên grv
							grvData.SetRowCellValue(i, "Lo", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
							string PartCode = TextUtils.ToString(grvData.GetRowCellValue(i, colArticleID));
							grvData.SetRowCellValue(i, "Accessory", PartCode);
						}
					}
				}
			}
			if (lstError.Count > 0)
			{
				contentError = string.Join("\n", lstError);
				//MessageBox.Show(contentError, "RTC", MessageBoxButtons.OK, MessageBoxIcon.Stop);
				//WarningForm frmWarning = new WarningForm();
				//frmWarning.LBTieuDe = "KHÔNG ĐỦ LINH KIỆN XUẤT KHO";
				//frmWarning.LB = $"{contentError}";
				//frmWarning.ShowDialog();
			}
			else
			{
				if (grvData.RowCount == _lstCount.Count)
				{
					btnSave_Click(null, null);
					return;
				}
			}

		}
		private void setFocus()
		{
			if (cboKho.Text.Trim().Contains("KT"))
			{
				txtLocation.Focus();
			}
			else
				txtPartCode.Focus();
			txtOrderr.BackColor = Color.White;
			txtQtyAssembleStock.BackColor = Color.White;
			txtTargetAssembleStock.BackColor = Color.White;
			txtPidAssembleStock.BackColor = Color.White;
			txtDescriptionAssembleStock.BackColor = Color.White;
			txtStockCD.BackColor = Color.White;
			if (_focusbtnSave == 1)
			{
				//btnSave.Focus();
				btnSave_Click(null, null);
				_focusbtnSave = 0;
			}
		}
		private void txtAccessoryChecking_Click(object sender, EventArgs e)
		{
			txtPartCode.SelectAll();
		}

		private void txtNumLot_Click(object sender, EventArgs e)
		{
			txtNumLot.SelectAll();
		}

		private void txtPartCode_KeyDown(object sender, KeyEventArgs e)
		{
			if (txtPartCode.Text.Trim() == "") return;
			if (e.KeyCode != Keys.Enter) return;
			PartCode = txtPartCode.Text.Trim();
			//Check riêng Kho trong
			if (cboKho.Text.Trim().Contains("KT"))
			{
				if (txtLocation.Text.Trim() == "")
				{
					WarningForm frmWarning = new WarningForm();
					frmWarning.LB = "BẠN CHƯA NHẬP VỊ TRÍ";
					frmWarning.NameKho = cboKho.Text.Trim();
					frmWarning.Order = txtOrderr.Text.Trim();
					frmWarning.ShowDialog();
					return;
				}

				//Check mã hàng so với vị trí đã nhập trước
				int check = 0;
				for (int i = 0; i < _lstRow.Count; i++)
				{
					string ArticleID = TextUtils.ToString(grvData.GetRowCellValue(_lstRow[i], "ArticleID"));
					if (ArticleID.Trim().ToUpper() == PartCode.Trim().ToUpper())
					{
						check = 1;
						_row = _lstRow[i];
						break;
					}
				}
				if (check == 0)
				{
					WarningForm frmWarning = new WarningForm();
					frmWarning.LB = "SAI MÃ HÀNG SO VỚI VỊ TRÍ ĐÃ NHẬP";
					frmWarning.NameKho = cboKho.Text.Trim();
					frmWarning.Order = txtOrderr.Text.Trim();
					frmWarning.ShowDialog();
					txtPartCode.Focus();
					txtPartCode.SelectAll();
					return;
				}

			}
			else
			{
				try
				{
					PartCode = PartCode.Split(',')[0];
				}
				catch
				{

				}
			}

			_row = -1;
			for (int i = 0; i < _dtOrderPart.Rows.Count; i++)
			{
				string data = TextUtils.ToString(grvData.GetRowCellValue(i, "ArticleID"));
				if (cboKho.Text.Trim().ToUpper().Contains("MOTOR"))
				{
					if (LoadColorMotor(PartCode.Trim()).Trim().ToUpper() == data.Trim().ToUpper())
					{
						_row = i;
						break;
					}
					else
					{
						continue;
					}
				}
				if (PartCode.ToUpper() != "OK")
				{
					//nếu là công đoạn không phải motor
					if (cboKho.Text.ToUpper().Contains("CASSE") || cboKho.Text.ToUpper().Contains("KN"))
					{
						//bỏ T và A trong kho Casse trong 2 line 
						if (data.Substring(data.Length - 1).ToUpper() == "T" || data.Substring(data.Length - 1).ToUpper() == "A")
						{
							data = data.TrimEnd(data[data.Length - 1]);
						}
					}
					if (data.Trim().ToUpper() == PartCode.Trim().ToUpper())
					{
						_row = i;
						break;
					}
				}
				else
				{
					if (TextUtils.ToInt(grvData.GetRowCellValue(i, colRealQty)) == TextUtils.ToInt(grvData.GetRowCellValue(i, colQty)))
					{
						if (_dtOrderPart.Rows.Count - 1 == i)
						{
							btnSave_Click(null, null);
							txtPartCode.Text = "";
							return;
						}
						continue;
					}
					else
					{
						_row = i;
						break;
					}

				}
			}
			//DataRow[] rows = _dtOrderPart.Select($"ArticleID = '{txtPartCode.Text.Trim()}'");
			if (_row == -1)
			{
				WarningForm frmWarning = new WarningForm();
				frmWarning.NameKho = cboKho.Text.Trim();
				frmWarning.Order = txtOrderr.Text.Trim();
				frmWarning.LB = "SAI MÃ HÀNG";
				frmWarning.ShowDialog();
				txtPartCode.Text = "";
				txtPartCode.Focus();
				txtPartCode.SelectAll();
				//MessageBox.Show("Linh kiện không tồn tại trong Order!", "RTC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			if (cboKho.Text.Trim() == "KCasse" || cboKho.Text.Trim() == "KMotor" || cboKho.Text.Trim().Contains("KN"))
			{
				int QtyReal = TextUtils.ToInt(grvData.GetRowCellValue(_row, colRealQty));
				int ColumnName = 6;
				//Hiển thị mã bắn lên grid view
				if (cboLine.SelectedIndex == 1)
				{
					ColumnName = 6;
				}
				else if (cboLine.SelectedIndex == 2)
				{
					ColumnName = 7;
				}
				for (int i = 1; i < ColumnName; i++)
				{
					string Column = "Column";
					string valueColumn = TextUtils.ToString(grvData.GetRowCellValue(_row, Column + i)).ToUpper();
					if (PartCode.ToUpper() != "OK")
					{
						if (valueColumn == txtPartCode.Text.Trim().ToUpper())
						{
							txtPartCode.Text = "";
							txtPartCode.Focus();
							txtPartCode.SelectAll();
							return;
						}
						if (valueColumn != "" || i > TextUtils.ToInt(grvData.GetRowCellValue(_row, colQty))) continue;
						grvData.SetRowCellValue(_row, Column + i, txtPartCode.Text.Trim().ToUpper());
					}
					else
					{
						if (valueColumn.ToUpper() == "OK" || valueColumn != "") continue;
						grvData.SetRowCellValue(_row, Column + i, PartCode.ToUpper());

					}
					QtyReal++;
					grvData.SetRowCellValue(_row, "RealQty", QtyReal);
					break;
				}
				if (cboKho.Text.Contains("KN"))
				{
					if (TextUtils.ToInt(txtQtyAssembleStock.Text.Trim()) < QtyReal)
					{
						//btnSave_Click(null, null);
						return;
					}
				}
				//Check số lượng thực tế nếu bằng vs số lượng quy định thì dừng lại 
				else if (TextUtils.ToInt(grvData.GetRowCellValue(_row, colQty)) < QtyReal)
				{
					//btnSave_Click(null, null);
					txtPartCode.Text = "";
					txtPartCode.Focus();
					txtPartCode.SelectAll();
					return;
				}

				colRealQty.OptionsColumn.AllowEdit = false;
				txtNumLot.Enabled = true;
				//Gửi mã bắn và số lượng để trừ luôn giá trị kho
				if (cboKho.Text.ToUpper().Contains("MOTOR"))
				{
					try
					{
						DataTable dt = TextUtils.GetDataTableFromSP("spLoadSLShelf", new string[] { "@CardNo" }, new object[] { PartCode.Trim() });
						//if (dt.Rows.Count <= 0) return;
						ShelfModel shelfModel = new ShelfModel();
						shelfModel = (ShelfModel)ShelfBO.Instance.FindByAttribute("ShelfCode", TextUtils.ToString(dt.Rows[0]["Shelf"]).Trim())[0];
						shelfModel.ShelfNumber = shelfModel.ShelfNumber - 1;
						if (shelfModel.ShelfNumber < 0)
						{
						}
						else
						{
							ShelfBO.Instance.Update(shelfModel);
						}
						//Xóa trường trong bảng AutoAddMotor
						TextUtils.ExcuteSQL($" DELETE [ShiStock].[dbo].[AutoAddShelfMotor] WHERE SerialNumber=N'{PartCode.Trim()}'");
						for (int i = 0; i < grvData.RowCount; i++)
						{
							if (shelfModel.ShelfNumber == 0 && TextUtils.ToInt(grvData.GetRowCellValue(i, colRealQty)) != TextUtils.ToInt(grvData.GetRowCellValue(i, colQty)))
							{
								//Tìm vị trí tiếp theo
								string ArticleID = TextUtils.ToString(grvData.GetRowCellValue(i, colArticleID));
								DataTable dtt = TextUtils.GetDataTableFromSP("spLoadHistoryShelf", new string[] { "@ArticleID" }, new object[] { ArticleID });
								if (dt.Rows.Count > 0)
								{
									//Hiển thị vị trí lên cột vị trí trong gridview
									grvData.SetRowCellValue(i, colShelf, TextUtils.ToString(dt.Rows[0][0]));
								}
							}
						}
					}
					catch (Exception)
					{
						//MessageBox.Show("Lỗi update số lượng giá", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					}

				}
				if (cboKho.Text.Contains("KN"))
				{
					if (TextUtils.ToInt(txtQtyAssembleStock.Text.Trim()) == QtyReal)
					{
						if (grvData.RowCount == _lstCount.Count)
						{
							btnSave_Click(null, null);
							//btnSave.Focus();
							return;
						}
					}
				}
				else if (TextUtils.ToInt(grvData.GetRowCellValue(_row, colQty)) >= QtyReal)
				{
					if (grvData.RowCount == _lstCount.Count)
					{
						btnSave_Click(null, null);
						//btnSave.Focus();
						return;
					}
				}

				txtPartCode.Text = "";
				txtPartCode.Focus();
				txtPartCode.SelectAll();
				return;
			}
			//kHO trong

			colRealQty.OptionsColumn.AllowEdit = true;
			grvData.SetRowCellValue(_row, "Accessory", PartCode.Trim());
			txtQtyAccessory.Text = TextUtils.ToString(grvData.GetRowCellValue(_row, "Qty"));
			//string _textCompare = TextUtils.ToString(grvData.GetRowCellValue(_row, "ArticleID"));

			//checkLocation(row);
			txtNumLot.Focus();
			txtNumLot.SelectAll();
		}
		string LoadColorMotor(string CardNo)
		{
			string _AritelID = TextUtils.ToString(TextUtils.ExcuteScalar($"SELECT TOP 1 ArticleID FROM [SystemData].[dbo].[CheckMotor] WHERE MotorInspSealNo=N'{CardNo}' or CardNo=N'{CardNo}'"));
			return _AritelID;
		}
		private void txtNumLot_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode != Keys.Enter) return;
			_row = -1;
			string data = "";
			string Numer = "";
			for (int i = 0; i < _dtOrderPart.Rows.Count; i++)
			{
				data = TextUtils.ToString(grvData.GetRowCellValue(i, "ArticleID"));
				Numer = TextUtils.ToString(grvData.GetRowCellValue(i, colQty));
				if (data.ToUpper() == PartCode.Trim().ToUpper())
				{
					_row = i;
					break;
				}
			}

			//HIển thị form xác nhận số lượng
			frmSL frm = new frmSL();
			frm.PartCode = data;
			frm.numer = Numer;
			if (frm.ShowDialog() != DialogResult.OK)
			{
				txtNumLot.Focus();
				txtNumLot.SelectAll();
				return;
			}
			else
			{
				grvData.SetRowCellValue(_row, "Lo", txtNumLot.Text.Trim().ToUpper());
				txtLocation.Focus();
				txtLocation.SelectAll();
				txtLocation.Text = "";
				txtNumLot.Text = "";
				txtPartCode.Text = "";
				txtQtyAccessory.Text = "";

				if (grvData.RowCount == _lstCount.Count)
				{
					btnSave_Click(null, null);
					//	btnSave.Focus();
					return;
				}
			}

		}
		private void cấtToolStripMenuItem_Click(object sender, EventArgs e)
		{
			btnSave_Click(null, null);
		}
		void Reset()
		{
			txtPidAssembleStock.Text = "";
			txtDescriptionAssembleStock.Text = "";
			txtOrderr.Text = "";
			txtQtyAssembleStock.Text = "";
			txtTargetAssembleStock.Text = "";
			txtPartCode.Text = "";
			txtNumLot.Text = "";
			txtQtyAccessory.Text = "";
			grdData.DataSource = null;
			txtOrderr.Focus();
			txtOrderr.SelectAll();
			txtLocation.Text = "";
		}
		async void saveDetail(List<PartOutDetailModel> lstDetail)
		{
			Task task = Task.Factory.StartNew(() =>
			{
				foreach (PartOutDetailModel item in lstDetail)
				{
					PartOutDetailBO.Instance.Insert(item);
				}
			});
			await task;
		}
		async void saveHistory(List<SONHistoryImExModel> lstDetail)
		{
			Task task = Task.Factory.StartNew(() =>
			{
				foreach (SONHistoryImExModel item in lstDetail)
				{
					SONHistoryImExBO.Instance.Insert(item);
				}
			});
			await task;
		}


		private void txtWorkerCode_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode != Keys.Enter) return;
			txtWorkerCode.BackColor = Color.White;
			cboKho.Focus();
		}

		private void KhaiBaoToolStripMenuItem_Click(object sender, EventArgs e)
		{
			frmStockCD frm = new frmStockCD();
			if (frm.ShowDialog() == DialogResult.OK)
			{
				loadCboStock();
			}
		}

		private void cboKho_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (cboKho.Text.Trim() == "") return;
			if (cboKho.Text.Trim() == null) return;
			if (cboKho.Text.Trim().ToUpper().Contains("KT"))
			{
				colColumn1.Visible = false;
				colColumn2.Visible = false;
				colColumn3.Visible = false;
				colColumn4.Visible = false;
				colColumn5.Visible = false;
				colColumn6.Visible = false;
				button4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
				tableLayoutPanel1.SetColumn(txtPartCode, 3);
				tableLayoutPanel1.SetColumn(button4, 2);
				button1.Visible = true;
				txtLocation.Visible = true;
				txtSoLot.Visible = true;
				btnSL.Visible = true;
				txtQtyAccessory.Visible = true;
				tableLayoutPanel1.SetColumnSpan(txtPartCode, 1);
				colLoLot.Visible = true;
				colRealQty.Visible = true;
				txtNumLot.Visible = true;
			}
			else
			{
				if (cboLine.SelectedIndex == 2)
					colColumn6.Visible = true;
				colColumn5.Visible = true;
				colColumn4.Visible = true;
				colColumn3.Visible = true;
				colColumn2.Visible = true;
				colColumn1.Visible = true;
				txtSoLot.Visible = false;
				txtNumLot.Visible = false;
				btnSL.Visible = false;
				button1.Visible = false;
				txtLocation.Visible = false;
				txtQtyAccessory.Visible = false;
				tableLayoutPanel1.SetColumn(txtPartCode, 1);
				tableLayoutPanel1.SetColumn(button4, 0);
				tableLayoutPanel1.SetColumnSpan(txtPartCode, 7);
				button4.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
				colRealQty.Visible = false;
				colLoLot.Visible = false;

			}
			txtOrderr.Focus();
			if (_threadLoadAll != null) _threadLoadAll.Abort();
			loadStockCD();
		}
		/// <summary>
		/// Load liên tục khi kho casse và motor để tìm ra order
		/// </summary>

		private void cboKho_KeyDown(object sender, KeyEventArgs e)
		{

			if (e.KeyCode != Keys.Enter) return;
			if (cboKho.Text.Trim() == "") return;
			if (cboKho.Text.Trim() == "KCasse" || cboKho.Text.Trim() == "KMotor")
			{
				txtSoLot.Visible = false;
				txtNumLot.Visible = false;
				btnSL.Visible = false;
				txtQtyAccessory.Visible = false;
				tableLayoutPanel1.SetColumnSpan(txtPartCode, 5);
			}
			else
			{
				txtSoLot.Visible = true;
				txtNumLot.Visible = true;
				btnSL.Visible = true;
				txtQtyAccessory.Visible = true;
				tableLayoutPanel1.SetColumnSpan(txtPartCode, 1);
			}
			Reset();
		}
		private void frmXK_FormClosed(object sender, FormClosedEventArgs e)
		{
			if (_threadLoadAll != null) _threadLoadAll.Abort();
		}

		private void sựCốToolStripMenuItem_Click(object sender, EventArgs e)
		{
			this.BackColor = Color.Red;
			//Gửi tín hiệu risk qua server Andon qua TCP/IP
			sendDataTCPAnDonPicking("Error", "XK");
		}

		private void cboKho_Click(object sender, EventArgs e)
		{
			if (_threadLoadAll != null) _threadLoadAll.Abort();
		}



		/// <summary>
		/// F8 không sử dụng và ngược lại
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void khôngSửDụngToolStripMenuItem1_Click(object sender, EventArgs e)
		{
			if (khôngSửDụngToolStripMenuItem1.Text.Trim() == "Không Sử Dụng")
			{
				sendDataTCPAnDonPicking("KSD", "XK");
				khôngSửDụngToolStripMenuItem1.Text = "Sử dụng";
				this.Enabled = false;

			}
			else
			{
				sendDataTCPAnDonPicking("SD", "XK");
				khôngSửDụngToolStripMenuItem1.Text = "Không Sử Dụng";
				_startMakeTime = DateTime.Now;
				this.Enabled = true;
			}

		}

		private void khôngSửDụngToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				frmChooseRisk frm = new frmChooseRisk();
				if (frm.ShowDialog() == DialogResult.OK)
				{
					PartOutModel partOut = new PartOutModel();
					partOut.StockID = TextUtils.ToInt(cboKho.SelectedValue);
					partOut.WorkerName = txtWorkerCode.Text.Trim();
					partOut.LineName = cboLine.Text.Trim();
					partOut.OrderCode = txtOrderr.Text.Trim();
					partOut.PidAssembleStock = txtPidAssembleStock.Text.Trim();
					partOut.DescriptionAssembleStock = txtDescriptionAssembleStock.Text.Trim();
					partOut.StockCode = cboKho.Text.Trim();
					partOut.CreatDate = DateTime.Now;
					partOut.Status = false;
					partOut.StartTime = _startMakeTime;
					_endMakeTime = DateTime.Now;
					partOut.EndTime = _endMakeTime;
					partOut.RiskDescription = frm.RiskDescription;
					partOut.PeriodTime = TextUtils.ToInt(Math.Round((_endMakeTime - _startMakeTime).TotalSeconds, 0));
					partOut.Type = 2;
					PartOutBO.Instance.Insert(partOut);
					sendDataTCPAnDonPicking("OK", "XK");
					this.BackColor = Color.White;
				}
			}
			catch
			{

			}
		}

		private void btnSave_Click(object sender, EventArgs e)
		{
			if (txtWorkerCode.Text.Trim() == "" || txtOrderr.Text.Trim() == "" || cboKho.Text.Trim() == "")
			{
				txtPartCode.Text = "";
				return;
			}
			int row = grvData.RowCount;
			if (row <= 0) return;
			if (cboKho.Text.Trim() == "KMotor" || cboKho.Text.Trim() == "KCasse")
			{

				for (int i = 0; i < row; i++)
				{
					if (Lib.ToInt(grvData.GetRowCellValue(i, colQty)) > Lib.ToInt(grvData.GetRowCellValue(i, colRealQty)))
					{
						MessageBox.Show("Bạn phải nhập đủ số lượng", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
						return;
					}
				}
			}
			//TODO: Check các kho trừ kho ngoài thì được cất
			if (cboKho.Text.Trim() == "KMotor" || cboKho.Text.Trim() == "KCasse" || cboKho.Text.Trim().Contains("KT"))
			{
				if (row != _lstCount.Count)
				{
					MessageBox.Show("Bạn phải nhập đầy đủ thông tin", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}
			}

			//Hiển thị có cất hay không và hiển thị số lượng thực tế và số lượng kế hoạch
			frmSaveOK frm = new frmSaveOK();
			frm._QtyReal = grvData.RowCount;
			frm._QtyPlan = grvData.RowCount;
			if (frm.ShowDialog() == DialogResult.Cancel)
			{
				txtPartCode.Text = "";
				return;
			}
			// Lưu master Xuất kho
			PartOutModel partOut = new PartOutModel();
			try
			{
				partOut.StockID = TextUtils.ToInt(cboKho.SelectedValue);
				partOut.WorkerName = txtWorkerCode.Text.Trim();
				partOut.LineName = cboLine.Text.Trim();
				partOut.OrderCode = txtOrderr.Text.Trim();
				partOut.PidAssembleStock = txtPidAssembleStock.Text.Trim();
				partOut.DescriptionAssembleStock = txtDescriptionAssembleStock.Text.Trim();
				partOut.StockCode = cboKho.Text.Trim();
				partOut.CreatDate = DateTime.Now;
				partOut.Status = false;
				partOut.StockCDCode = txtStockCD.Text.Trim();
				partOut.StartTime = _startMakeTime;
				_endMakeTime = DateTime.Now;
				partOut.EndTime = _endMakeTime;
				partOut.PeriodTime = TextUtils.ToInt(Math.Round((_endMakeTime - _startMakeTime).TotalSeconds, 0));
				if (partOut.PeriodTime > 0 && partOut.PeriodTime < 200)
				{
					partOut.Type = 4;
				}
				else
				{
					partOut.Type = 4;
				}
				partOut.ID = (int)PartOutBO.Instance.Insert(partOut);
			}
			catch (Exception ex)
			{
				ErrorLog.errorLog("Thêm PartOut", "Lỗi InSert Part Out", Environment.NewLine);
			}


			List<PartOutDetailModel> lstDetail = new List<PartOutDetailModel>();
			try
			{
				for (int i = 0; i < grvData.RowCount; i++)
				{
					//Lưu detail Xuất kho
					PartOutDetailModel partOutDetail = new PartOutDetailModel();
					partOutDetail.PartOutID = partOut.ID;
					partOutDetail.Description = TextUtils.ToString(grvData.GetRowCellValue(i, colDescription));
					partOutDetail.ArticleID = TextUtils.ToString(grvData.GetRowCellValue(i, colArticleID));
					partOutDetail.Shelf = TextUtils.ToString(grvData.GetRowCellValue(i, colShelf));
					partOutDetail.Qty = TextUtils.ToInt(grvData.GetRowCellValue(i, colQty));
					partOutDetail.Lo = TextUtils.ToString(grvData.GetRowCellValue(i, colLoLot));
					partOutDetail.Column1 = TextUtils.ToString(grvData.GetRowCellValue(i, colColumn1));
					partOutDetail.Column2 = TextUtils.ToString(grvData.GetRowCellValue(i, colColumn2));
					partOutDetail.Column3 = TextUtils.ToString(grvData.GetRowCellValue(i, colColumn3));
					partOutDetail.Column4 = TextUtils.ToString(grvData.GetRowCellValue(i, colColumn4));
					partOutDetail.Column5 = TextUtils.ToString(grvData.GetRowCellValue(i, colColumn5));
					partOutDetail.Column6 = TextUtils.ToString(grvData.GetRowCellValue(i, colColumn6));
					partOutDetail.Accessory = TextUtils.ToString(grvData.GetRowCellValue(i, colRealValue));
					partOutDetail.RealQty = TextUtils.ToInt(grvData.GetRowCellValue(i, colRealQty));
					partOutDetail.CreatDate = DateTime.Now;
					lstDetail.Add(partOutDetail);
				}
			}
			catch (Exception ex)
			{
				ErrorLog.errorLog("Thêm partOutDetail", $"Lỗi InSert partOutDetail {ex.ToString()}", Environment.NewLine);
			}
			//try
			//{
			//	//Xóa 2 bảng tạm Run Casse và Run Motor và Update check dữ liệu AddAutoXK khi đang xuất kho casse hoặc motor
			//	if (cboKho.Text.Trim() == "KCasse" || cboKho.Text.Trim() == "KMotor")
			//	{
			//		TextUtils.ExcuteProcedure("spDeleteCasseOrMotor",
			//									new string[] { "@Check", "@Order" },
			//									new object[] { cboKho.Text.Trim(), txtOrderr.Text.Trim() });
			//	}
			//}
			//catch (Exception ex)
			//{
			//	ErrorLog.errorLog("spDeleteCasseOrMotor", $" {ex.ToString()}", Environment.NewLine);
			//}
			if (cboKho.Text.Trim().Contains("KT") || cboKho.Text.Trim().Contains("KN"))
				sendDataTCPAnDonPicking(txtOrderr.Text.Trim(), $"Delete {txtQtyAssembleStock.Text.Trim()} {cboKho.Text.Trim()}");
			else if (cboKho.Text.Trim().ToUpper().Contains("KCASSE"))
			{
				sendDataTCPAnDonPicking(_Casse, $"Delete {txtQtyAssembleStock.Text.Trim()} {cboKho.Text.Trim()}");
			}
			else if (cboKho.Text.Trim().ToUpper().Contains("KMOTOR"))
			{
				sendDataTCPAnDonPicking(_Motor, $"Delete {txtQtyAssembleStock.Text.Trim()} {cboKho.Text.Trim()}");
			}
			//frmSaveOK frm = new frmSaveOK();
			//frm.Show();
			//Update trạng thái vào kế hoạch hyp và altax 
			int Stock = 0;//1 KT,2 KN
			try
			{
				if (cboKho.Text.Trim().ToUpper().Contains("KT"))
				{
					Stock = 1;
				}
				else if (cboKho.Text.Trim().ToUpper().Contains("KN"))
				{
					Stock = 2;
				}
				if (cboKho.Text.Trim().ToUpper().Contains("KN") || cboKho.Text.Trim().ToUpper().Contains("KT"))
					TextUtils.ExcuteProcedure("spUpdateShowPlan", new string[] { "@Stock", "@Order", "@Line" }, new object[] { Stock, txtOrderr.Text.Trim(), cboLine.Text.Trim().ToUpper().Contains("HYP") == true ? 1 : 0 });//1 line Hyp, 0 Line altax
			}
			catch (Exception ex)
			{
				ErrorLog.errorLog("spUpdateShowPlan", $" {ex.ToString()}", Environment.NewLine);
			}
			saveDetail(lstDetail);
			//saveHistory(lstHistory);
			Reset();
			_lstCount.Clear();
			_lstID.Clear();
			_lstArticleID.Clear();
		}
		private void txtLocation_KeyDown(object sender, KeyEventArgs e)
		{
			_lstRow.Clear();
			if (txtLocation.Text.Trim() == "") return;
			if (e.KeyCode != Keys.Enter) return;
			_row = -1;
			for (int i = 0; i < _dtOrderPart.Rows.Count; i++)
			{
				string data = TextUtils.ToString(grvData.GetRowCellValue(i, "Shelf"));
				if (data.ToUpper() == txtLocation.Text.Trim().ToUpper())
				{
					_row = i;
					_lstRow.Add(_row);
					//break;
				}
			}
			//DataRow[] rows = _dtOrderPart.Select($"ArticleID = '{txtPartCode.Text.Trim()}'");
			if (_row == -1)
			{
				WarningForm frmWarning = new WarningForm();
				frmWarning.NameKho = cboKho.Text.Trim();
				frmWarning.Order = txtOrderr.Text.Trim();
				frmWarning.LB = "SAI VỊ TRÍ";
				frmWarning.ShowDialog();
				txtLocation.Focus();
				txtLocation.SelectAll();
				//MessageBox.Show("Linh kiện không tồn tại trong Order!", "RTC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			txtPartCode.Focus();
			txtPartCode.SelectAll();
		}

		private void cboKho_SelectedValueChanged(object sender, EventArgs e)
		{

		}
		/// <summary>
		/// đổi màu xanh với những dòng đã điền đầy đủ
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void grvData_RowStyle(object sender, DevExpress.XtraGrid.Views.Grid.RowStyleEventArgs e)
		{
			if (cboKho.Text.Trim() == "KCasse" || cboKho.Text.Trim() == "KMotor")
			{
				if (Lib.ToInt(grvData.GetRowCellValue(e.RowHandle, colQty)) == Lib.ToInt(grvData.GetRowCellValue(e.RowHandle, colRealQty)) && Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colQty)) != "")
				{
					//e.Appearance.BackColor = Color.Lime;
					if (!_lstCount.Contains(e.RowHandle))
						_lstCount.Add(e.RowHandle);
				}
			}
			else if (cboKho.Text.Trim().Contains("KN"))
			{
				if (TextUtils.ToInt(txtQtyAssembleStock.Text.Trim()) == Lib.ToInt(grvData.GetRowCellValue(e.RowHandle, colRealQty)))
				{
					//e.Appearance.BackColor = Color.Lime;
					if (!_lstCount.Contains(e.RowHandle))
						_lstCount.Add(e.RowHandle);
				}
			}
			else if (Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colLoLot)) != "" && Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colRealValue)) != "" && Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colRealQty)) != "")
			{
				e.Appearance.BackColor = Color.Lime;
				if (!_lstCount.Contains(e.RowHandle))
					_lstCount.Add(e.RowHandle);

			}
		}
		private void grvData_RowCellStyle(object sender, DevExpress.XtraGrid.Views.Grid.RowCellStyleEventArgs e)
		{
			if ((e.Column == colColumn1 && Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colColumn1)) == "")
				|| (e.Column == colColumn2 && Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colColumn2)) == "")
				|| (e.Column == colColumn3 && Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colColumn3)) == ""
				|| (e.Column == colColumn4 && Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colColumn4)) == "")
				|| (e.Column == colColumn5 && Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colColumn5)) == "")
				|| (e.Column == colColumn6 && Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colColumn6)) == "")))
			{
				e.Appearance.BackColor = Color.FromArgb(255, 192, 255);
			}
			else if ((e.Column == colColumn1 && Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colColumn1)) != "")
				|| (e.Column == colColumn2 && Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colColumn2)) != "")
				|| (e.Column == colColumn3 && Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colColumn3)) != ""
				|| (e.Column == colColumn4 && Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colColumn4)) != "")
				|| (e.Column == colColumn5 && Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colColumn5)) != "")
				|| (e.Column == colColumn6 && Lib.ToString(grvData.GetRowCellValue(e.RowHandle, colColumn6)) != "")))
			{
				e.Appearance.BackColor = Color.FromArgb(102, 255, 255);
			}
		}

		private void btnDeleteOrder_Click(object sender, EventArgs e)
		{
			try
			{
				TextUtils.ExcuteProcedure("spDeleteStock", new string[] { "@Order", "@Stock" }, new object[] { txtOrderr.Text.Trim(), cboKho.Text.Trim() });
				MessageBox.Show("Bỏ qua thành công", "Thông báo", MessageBoxButtons.OK);
				txtOrderr.Text = "";
			}
			catch
			{

			}
		}

		private void cboLine_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (cboLine.SelectedIndex == 1)
			{
				colColumn6.Visible = false;
			}
			else if (cboLine.SelectedIndex == 2)
			{
				colColumn6.Visible = true;
			}
			File.WriteAllText(Application.StartupPath + "\\Line.txt", TextUtils.ToString(cboLine.SelectedIndex));
		}
	}
}
