using System;
using System.IO;
using System.Net;
using System.Web;
using Zeghs.Utils;

namespace Zeghs.Services {
	/// <summary>
	///   取得商品開高低收資訊(新版請求格式)
	/// </summary>
	public class apiGetSeriesSymbolData : IHttpHandler {
		private const int DATA_BLOCK_SIZE = 48;
		private const int DAY_TIMEFRAME_FORMAT = 86400;

		private const int INFORMATION_SUCCESS = 0;
		private const int ERROR_EXCEPTION = -1;
		private const int ERROR_FILE_NOT_FOUND = -2;

		public void ProcessRequest(HttpContext context) {
			HttpResponse cResponse = context.Response;
			cResponse.ContentType = "application/octet-stream";

			HttpRequest cRequest = context.Request;

			string sExchange = cRequest.Form["exchange"];  //交易所簡稱
			string sSymbolId = cRequest.Form["symbolId"];  //商品代號
			int iTimeFrame = int.Parse(cRequest.Form["timeFrame"]);  //時間週期(60=1分, 300=5分)
			long lPosition = long.Parse(cRequest.Form["position"]);  //客戶端目前歷史資料的檔案位置
			DateTime cStartDate = DateTime.Parse(cRequest.Form["startDate"]);  //資料起始時間
			DateTime cEndDate = DateTime.Parse(cRequest.Form["endDate"]);  //資料結束時間
			int iCount = int.Parse(cRequest.Form["count"]);  //資料請求個數

			string sTimeFrameFormat = (iTimeFrame < DAY_TIMEFRAME_FORMAT) ? "mins" : "days";
			string sFilename = context.Server.MapPath(string.Format("~/data/{0}/{1}/{2}", sExchange, sTimeFrameFormat, sSymbolId));
			if (File.Exists(sFilename)) {
				using (FileStream cStream = new FileStream(sFilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
					try {
						long lStartPosition = 0, lEndPosition = lPosition * DATA_BLOCK_SIZE;
						int iBlockCount = (int) (cStream.Length / DATA_BLOCK_SIZE);
						iCount = ((iCount < iBlockCount) ? iCount : iBlockCount);
						if (iCount == 0) {
							if (lPosition == -1) {
								FileSearchUtil.BinaryNearSearch(cStream, iBlockCount, DATA_BLOCK_SIZE, cEndDate.AddSeconds(86400));
								lEndPosition = cStream.Position;
							}

							FileSearchUtil.BinaryNearSearch(cStream, iBlockCount, DATA_BLOCK_SIZE, cStartDate);
							lStartPosition = cStream.Position;

							iCount = (int) ((lEndPosition - lStartPosition) / DATA_BLOCK_SIZE);
						} else {
							if (lPosition == -1) {
								FileSearchUtil.BinaryNearSearch(cStream, iBlockCount, DATA_BLOCK_SIZE, cEndDate.AddSeconds(86400));
								lEndPosition = cStream.Position;
							}

							lStartPosition = lEndPosition - iCount * DATA_BLOCK_SIZE;
							lStartPosition = (lStartPosition < 0) ? 0 : lStartPosition;

							iCount = (int) ((lEndPosition - lStartPosition) / DATA_BLOCK_SIZE);
						}
						lPosition = lStartPosition / DATA_BLOCK_SIZE;

						int iSize = iCount * DATA_BLOCK_SIZE;
						byte[] bData = new byte[iSize];

						cStream.Position = lStartPosition;
						iSize = cStream.Read(bData, 0, iSize);
						int iBeginDate = BitConverter.ToInt32(bData, 0);  //起始日期
						int iEndDate = BitConverter.ToInt32(bData, iSize - DATA_BLOCK_SIZE);  //終止日期

						cResponse.Cookies.Add(new HttpCookie("result", INFORMATION_SUCCESS.ToString()));
						cResponse.Cookies.Add(new HttpCookie("position", lPosition.ToString()));
						cResponse.Cookies.Add(new HttpCookie("count", iCount.ToString()));
						cResponse.Cookies.Add(new HttpCookie("dataSize", iSize.ToString()));
						cResponse.Cookies.Add(new HttpCookie("beginDate", iBeginDate.ToString()));
						cResponse.Cookies.Add(new HttpCookie("endDate", iEndDate.ToString()));

						Stream cOutput = cResponse.OutputStream;
						cOutput.Write(bData, 0, iSize);
						cOutput.Flush();
					} catch {
						cResponse.Cookies.Add(new HttpCookie("result", ERROR_EXCEPTION.ToString()));
					}
				}
			} else {
				cResponse.Cookies.Add(new HttpCookie("result", ERROR_FILE_NOT_FOUND.ToString()));
			}
		}

		public bool IsReusable {
			get {
				return false;
			}
		}
	}
}