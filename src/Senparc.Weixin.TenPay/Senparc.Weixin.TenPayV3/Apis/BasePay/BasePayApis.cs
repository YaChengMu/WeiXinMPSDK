﻿#region Apache License Version 2.0
/*----------------------------------------------------------------

Copyright 2021 Jeffrey Su & Suzhou Senparc Network Technology Co.,Ltd.

Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
except in compliance with the License. You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software distributed under the
License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
either express or implied. See the License for the specific language governing permissions
and limitations under the License.

Detail: https://github.com/JeffreySu/WeiXinMPSDK/blob/master/license.md

----------------------------------------------------------------*/
#endregion Apache License Version 2.0

/*----------------------------------------------------------------
    Copyright (C) 2021 Senparc
  
    文件名：BasePayApis.cs
    文件功能描述：新微信支付V3基础接口
    
    
    创建标识：Senparc - 20210804

    修改标识：Senparc - 20210811
    修改描述：完成JsApi支付签名方法
    
----------------------------------------------------------------*/

using Senparc.CO2NET.Helpers;
using Senparc.CO2NET.HttpUtility;
using Senparc.Weixin.TenPayV3.Apis.BasePay;
using Senparc.Weixin.TenPayV3.Apis.BasePay.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Senparc.NeuChar.Helpers;

namespace Senparc.Weixin.TenPayV3.Apis
{
    public static class BasePayApis
    {
        //private readonly IServiceProvider _serviceProvider;

        //public BasePayApis(IServiceProvider serviceProvider)
        //{
        //    this._serviceProvider = serviceProvider;
        //}

        /// <summary>
        /// 返回可用的微信支付地址（自动判断是否使用沙箱）
        /// </summary>
        /// <param name="urlFormat">如：<code>https://api.mch.weixin.qq.com/{0}pay/unifiedorder</code></param>
        /// <returns></returns>
        private static string ReurnPayApiUrl(string urlFormat)
        {
            return string.Format(urlFormat, Senparc.Weixin.Config.UseSandBoxPay ? "sandboxnew/" : "");
        }

        /// <summary>
        /// JSAPI下单接口
        /// 在微信支付服务后台生成 JSAPI 预支付交易单，返回预支付交易会话标识
        /// </summary>
        /// <param name="dataInfo">微信支付需要post的Data数据</param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static JsApiReturnJson JsApi(IServiceProvider serviceProvider, JsApiRequestData data,
            int timeOut = Config.TIME_OUT)
        {
            var url = ReurnPayApiUrl("https://api.mch.weixin.qq.com/{0}v3/pay/transactions/jsapi");
            var jsonString = SerializerHelper.GetJsonString(data, null);
            using (MemoryStream ms = new MemoryStream())
            {
                var bytes = Encoding.UTF8.GetBytes(jsonString);
                ms.Write(bytes, 0, bytes.Length);
                ms.Seek(0, SeekOrigin.Begin);

                WeixinTrace.SendApiPostDataLog(url, jsonString); //记录Post的Json数据

                //PostGetJson方法中将使用WeixinTrace记录结果
                return Post.PostGetJson<JsApiReturnJson>(serviceProvider, url, null, ms,
                    timeOut: timeOut,
                    afterReturnText: null,
                    checkValidationResult: false);
            }
        }

        /// <summary>
        /// 获取UI使用的JS支付签名
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="timeStamp"></param>
        /// <param name="nonceStr"></param>
        /// <param name="package">格式：prepay_id={0}</param>
        /// <returns></returns>
        public static string GetJsApiPaySign(string appId, string timeStamp, string nonceStr, string package,
            string privateKey)
        {
            string contentForSign = $"{appId}\n{timeStamp}\n{nonceStr}\n{package}\n";

            byte[] keyData = Convert.FromBase64String(privateKey);
            using (CngKey cngKey = CngKey.Import(keyData, CngKeyBlobFormat.Pkcs8PrivateBlob))
            using (RSACng rsa = new RSACng(cngKey))
            {
                byte[] data = System.Text.Encoding.UTF8.GetBytes(contentForSign);
                return Convert.ToBase64String(rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            }
        }

        /// <summary>
        /// 微信支付订单号查询
        /// </summary>
        /// <param name="authorization">请求authorization</param>
        /// <param name="transaction_id"> 微信支付系统生成的订单号 示例值：1217752501201407033233368018</param>
        /// <param name="mchid">直连商户的商户号，由微信支付生成并下发。 示例值：1230000109</param>
        /// <returns></returns>
        public static OrderJson OrderQueryByTransactionId(string authorization, string transaction_id, string mchid)
        {
            var urlFormat =
                ReurnPayApiUrl(
                    $"https://api.mch.weixin.qq.com/v3/{{0}}pay/transactions/id/{transaction_id}?mchid={mchid}");

            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", authorization);

            var response = httpClient.GetAsync(urlFormat);
            response.Wait();

            var responseBody = response.Result.Content.ReadFromJsonAsync<OrderJson>();
            responseBody.Wait();

            return responseBody.Result;
        }

        /// <summary>
        /// 商户订单号查询
        /// </summary>
        /// <param name="authorization">请求authorization</param>
        /// <param name="out_trade_no"> 微信支付系统生成的订单号 示例值：1217752501201407033233368018</param>
        /// <param name="mchid">直连商户的商户号，由微信支付生成并下发。 示例值：1230000109</param>
        /// <returns></returns>
        public static OrderJson OrderQueryByOutTradeNo(string authorization, string out_trade_no, string mchid)
        {
            var urlFormat =
                ReurnPayApiUrl(
                    $"https://api.mch.weixin.qq.com/v3/{{0}}pay/transactions/id/{out_trade_no}?mchid={mchid}");

            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", authorization);

            var response = httpClient.GetAsync(urlFormat);
            response.Wait();

            var responseBody = response.Result.Content.ReadFromJsonAsync<OrderJson>();
            responseBody.Wait();

            return responseBody.Result;
        }

        /// <summary>
        /// 关闭订单接口
        /// </summary>
        /// <param name="authorization">请求authorization</param>
        /// <param name="out_trade_no">商户系统内部订单号，只能是数字、大小写字母_-*且在同一个商户号下唯一 示例值：1217752501201407033233368018</param>
        /// <param name="mchid">直连商户的商户号，由微信支付生成并下发。 示例值：1230000109</param>
        /// <returns></returns>
        public static HttpStatusCode CloseOrder(string authorization, string out_trade_no, string mchid)
        {
            var urlFormat =
                ReurnPayApiUrl(
                    $"https://api.mch.weixin.qq.com/v3/{{0}}pay/transactions/out-trade-no/{out_trade_no}/close");

            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", authorization);

            HttpContent content = new StringContent(JsonConvert.SerializeObject(mchid));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            var response = httpClient.PostAsync(urlFormat, content);
            response.Wait();

            return response.Result.StatusCode;
        }

        /// <summary>
        /// 申请退款接口
        /// </summary>
        /// <param name="authorization">请求authorization</param>
        /// <param name="body">请求主体</param>
        /// <param name="timeOut">超时时间</param>
        /// <returns></returns>
        public static RefundResultData Refund(string authorization, string body, int timeOut = Config.TIME_OUT)
        {
            var urlFormat = ReurnPayApiUrl($"https://api.mch.weixin.qq.com/v3/{{0}}refund/domestic/refunds");
            HttpClient httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMilliseconds(timeOut);
            httpClient.DefaultRequestHeaders.Add("Authorization", authorization);
            HttpContent content = new StringContent(body);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            var response = httpClient.GetAsync(urlFormat);
            response.Wait();

            var responseBody = response.Result.Content.ReadFromJsonAsync<RefundResultData>();
            responseBody.Wait();

            return responseBody.Result;
        }

        /// <summary>
        /// 查询单笔退款接口
        /// </summary>
        /// <param name="authorization">请求authorization</param>
        /// <param name="out_refund_no">商户系统内部的退款单号，商户系统内部唯一，只能是数字、大小写字母_-|*@ ，同一退款单号多次请求只退一笔。示例值：1217752501201407033233368018</param>
        /// <returns></returns>
        public static RefundResultData RefundQuery(string authorization, string out_refund_no)
        {
            var urlFormat =
                ReurnPayApiUrl(
                    $"https://api.mch.weixin.qq.com/v3/{{0}}refund/domestic/refunds/{out_refund_no}");

            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", authorization);

            var response = httpClient.GetAsync(urlFormat);
            response.Wait();

            var responseBody = response.Result.Content.ReadFromJsonAsync<RefundResultData>();
            responseBody.Wait();

            return responseBody.Result;
        }

        /// <summary>
        /// 申请交易账单接口
        /// 获得微信支付按天提供的交易账单文件
        /// </summary>
        /// <param name="authorization">请求authorization</param>
        /// <param name="bill_date">账单日期 格式YYYY-MM-DD 仅支持三个月内的账单下载申请</param>
        /// <param name="bill_type">填则默认是ALL 枚举值：ALL：返回当日所有订单信息（不含充值退款订单）SUCCESS：返回当日成功支付的订单（不含充值退款订单）REFUND：返回当日退款订单（不含充值退款订单</param>
        /// <param name="tar_type"> 不填则默认是数据流 枚举值：GZIP：返回格式为.gzip的压缩包账单</param>
        /// <returns></returns>
        public static BillData TradeBillQuery(string authorization, string bill_date, string bill_type = "ALL", string tar_type = null)
        {
            var urlFormat =
                ReurnPayApiUrl(
                    $"https://api.mch.weixin.qq.com/v3/{{0}}bill/tradebill?bill_date={bill_date}&bill_type={bill_type}");
            if (tar_type != null)
            {
                urlFormat += $"&tar_type={tar_type}";
            }

            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", authorization);

            var response = httpClient.GetAsync(urlFormat);
            response.Wait();

            var responseBody = response.Result.Content.ReadFromJsonAsync<BillData>();
            responseBody.Wait();

            return responseBody.Result;
        }

        /// <summary>
        /// 申请资金账单接口
        /// 获得微信支付按天提供的微信支付账户资金流水账单文件
        /// </summary>
        /// <param name="signature">请求签名</param>
        /// <param name="bill_date">账单日期 格式YYYY-MM-DD 仅支持三个月内的账单下载申请</param>
        /// <param name="account_type">不填则默认是BASIC 枚举值：BASIC：基本账户 OPERATION：运营账户 FEES：手续费账户</param>
        /// <param name="tar_type"> 不填则默认是数据流 枚举值：GZIP：返回格式为.gzip的压缩包账单</param>
        /// <returns></returns>
        public static BillData FundflowBillQuery(string signature, string bill_date, string account_type = "BASIC", string tar_type = null)
        {
            var urlFormat =
                ReurnPayApiUrl(
                    $"https://api.mch.weixin.qq.com/v3/{{0}}bill/fundflowbill?bill_date={bill_date}&account_type={account_type}");
            if (tar_type != null)
            {
                urlFormat += $"&tar_type={tar_type}";
            }

            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", signature);

            var response = httpClient.GetAsync(urlFormat);
            response.Wait();

            var responseBody = response.Result.Content.ReadFromJsonAsync<BillData>();
            responseBody.Wait();

            return responseBody.Result;
        }

        /// <summary>
        /// APP支付下单接口
        /// 在微信支付服务后台生成 APP支付 预支付交易单，返回预支付交易会话标识
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="data">APP支付下单请求数据</param>
        /// <param name="timeOut">超时时间 单位ms</param>
        /// <returns></returns>
        public static AppReturnJson AppPay(IServiceProvider serviceProvider, AppRequestData data,
            int timeOut = Config.TIME_OUT)
        {
            var url = ReurnPayApiUrl("https://api.mch.weixin.qq.com/{0}v3/pay/transactions/app");
            var jsonString = SerializerHelper.GetJsonString(data, null);
            using (MemoryStream ms = new MemoryStream())
            {
                var bytes = Encoding.UTF8.GetBytes(jsonString);
                ms.Write(bytes, 0, bytes.Length);
                ms.Seek(0, SeekOrigin.Begin);

                WeixinTrace.SendApiPostDataLog(url, jsonString); //记录Post的Json数据

                //PostGetJson方法中将使用WeixinTrace记录结果
                return Post.PostGetJson<AppReturnJson>(serviceProvider, url, null, ms,
                    timeOut: timeOut,
                    afterReturnText: null,
                    checkValidationResult: false);
            }
        }

        /// <summary>
        /// H5支付下单接口
        /// 在微信支付服务后台生成 APP支付 预支付交易单，返回预支付交易会话标识
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="data">APP支付下单请求数据</param>
        /// <param name="timeOut">超时时间 单位ms</param>
        /// <returns></returns>
        public static H5ReturnJson H5Pay(IServiceProvider serviceProvider, H5RequestData data,
            int timeOut = Config.TIME_OUT)
        {
            var url = ReurnPayApiUrl("https://api.mch.weixin.qq.com/{0}v3/pay/transactions/h5");
            var jsonString = SerializerHelper.GetJsonString(data, null);
            using (MemoryStream ms = new MemoryStream())
            {
                var bytes = Encoding.UTF8.GetBytes(jsonString);
                ms.Write(bytes, 0, bytes.Length);
                ms.Seek(0, SeekOrigin.Begin);

                WeixinTrace.SendApiPostDataLog(url, jsonString); //记录Post的Json数据

                //PostGetJson方法中将使用WeixinTrace记录结果
                return Post.PostGetJson<H5ReturnJson>(serviceProvider, url, null, ms,
                    timeOut: timeOut,
                    afterReturnText: null,
                    checkValidationResult: false);
            }
        }

        /// <summary>
        /// Native支付下单接口
        /// 在微信支付服务后台生成 Native支付 预支付交易单，返回预支付交易会话标识
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="data">APP支付下单请求数据</param>
        /// <param name="timeOut">超时时间 单位ms</param>
        /// <returns></returns>
        public static NativeReturnJson NativePay(IServiceProvider serviceProvider, NativeRequestData data,
            int timeOut = Config.TIME_OUT)
        {
            var url = ReurnPayApiUrl("https://api.mch.weixin.qq.com/{0}v3/pay/transactions/native");
            var jsonString = SerializerHelper.GetJsonString(data, null);
            using (MemoryStream ms = new MemoryStream())
            {
                var bytes = Encoding.UTF8.GetBytes(jsonString);
                ms.Write(bytes, 0, bytes.Length);
                ms.Seek(0, SeekOrigin.Begin);

                WeixinTrace.SendApiPostDataLog(url, jsonString); //记录Post的Json数据

                //PostGetJson方法中将使用WeixinTrace记录结果
                return Post.PostGetJson<NativeReturnJson>(serviceProvider, url, null, ms,
                    timeOut: timeOut,
                    afterReturnText: null,
                    checkValidationResult: false);
            }
        }
    }
}
