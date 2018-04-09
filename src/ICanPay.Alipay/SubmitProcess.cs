﻿using ICanPay.Alipay.Response;
using ICanPay.Core;
using ICanPay.Core.Exceptions;
using ICanPay.Core.Request;
using ICanPay.Core.Response;
using ICanPay.Core.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace ICanPay.Alipay
{
    internal static class SubmitProcess
    {
        internal static TResponse Execute<TModel, TResponse>(Merchant merchant, Request<TModel, TResponse> request) where TResponse : IResponse
        {
            AddMerchant(merchant, request);

            string result = null;
            Task.Run(async () =>
            {
                result = await HttpUtil
                 .PostAsync(request.RequestUrl, request.GatewayData.ToUrl());
            })
            .GetAwaiter()
            .GetResult();

            var jObject = JObject.Parse(result);
            var jToken = jObject.First.First;
            string sign = jObject.Value<string>("sign");
            if (!CheckSign(jToken.ToString(Formatting.None), sign, merchant.AlipayPublicKey, merchant.SignType))
            {
                throw new GatewayException("签名验证失败");
            }

            var baseResponse = (BaseResponse)jToken.ToObject(typeof(TResponse));
            baseResponse.Raw = result;
            baseResponse.Sign = sign;
            baseResponse.Execute(merchant, request);
            return (TResponse)(object)baseResponse;
        }

        internal static TResponse SdkExecute<TModel, TResponse>(Merchant merchant, Request<TModel, TResponse> request) where TResponse : IResponse
        {
            AddMerchant(merchant, request);

            return (TResponse)Activator.CreateInstance(typeof(TResponse), request);
        }

        private static void AddMerchant<TModel, TResponse>(Merchant merchant, Request<TModel, TResponse> request) where TResponse : IResponse
        {
            request.GatewayData.Add(merchant, StringCase.Snake);
            if (!string.IsNullOrEmpty(request.NotifyUrl))
            {
                request.GatewayData.Add("notify_url", request.NotifyUrl);
            }
            if (!string.IsNullOrEmpty(request.ReturnUrl))
            {
                request.GatewayData.Add("return_url", request.ReturnUrl);
            }
            request.GatewayData.Add(Constant.SIGN, BuildSign(request.GatewayData, merchant.Privatekey, merchant.SignType));
        }

        internal static string BuildSign(GatewayData gatewayData, string privatekey, string signType)
        {
            return EncryptUtil.RSA(gatewayData.ToUrl(false), privatekey, signType);
        }

        internal static bool CheckSign(string data, string sign, string alipayPublicKey, string signType)
        {
            bool result = EncryptUtil.RSAVerifyData(data, sign, alipayPublicKey, signType);
            if (!result)
            {
                data = data.Replace("/", "\\/");
                result = EncryptUtil.RSAVerifyData(data, sign, alipayPublicKey, signType);
            }

            return result;
        }
    }
}
