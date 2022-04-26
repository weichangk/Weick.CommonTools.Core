using System;
using COSXML;
using COSXML.Auth;
using COSXML.Model.Object;
using COSXML.Model.Bucket;
using COSXML.CosException;
using COSXML.Utils;
using COSXML.Model.Service;
using COSXML.Transfer;
using COSXML.Model;
using System.Threading.Tasks;
using System.IO;

namespace Weick.CommonTools.Code.Helper
{
    public class CosBuilder
    {
        private CosXmlServer cosXml;
        private string appid;
        private string region;
        private CosXmlConfig cosXmlConfig;
        private QCloudCredentialProvider cosCredentialProvider;
        public CosBuilder(){}

        public CosBuilder SetAccount(string appid, string region)
        {
            this.appid = appid;
            this.region = region;
            return this;
        }
        public CosBuilder SetCosXmlServer(int ConnectionTimeoutMs = 60000, int ReadWriteTimeoutMs = 40000, bool IsHttps = true, bool SetDebugLog = true)
        {
            cosXmlConfig = new CosXmlConfig.Builder()
                .SetConnectionTimeoutMs(ConnectionTimeoutMs)
                .SetReadWriteTimeoutMs(ReadWriteTimeoutMs)
                .IsHttps(true)
                .SetAppid(this.appid)
                .SetRegion(this.region)
                .SetDebugLog(true)
                .Build();
            return this;
        }
        public CosBuilder SetSecret(string secretId, string secretKey, long durationSecond = 600)
        {
            cosCredentialProvider = new DefaultQCloudCredentialProvider(secretId, secretKey, durationSecond);
            return this;
        }
        public CosXmlServer Builder()
        {
            cosXml = new CosXmlServer(cosXmlConfig, cosCredentialProvider);
            return cosXml;
        }
    }

    public interface ICosClient
    {
        // 创建存储桶
        Task<CosResponseModel> CreateBucket(string buketName);

        // 获取存储桶列表
        Task<CosResponseModel> SelectBucket(int tokenTome = 600);
    }

    public interface IBucketClient
    {
        // 上传文件
        Task<CosResponseModel> UpFile (string key, string srcPath, string contentType = null);

        // 上传 base64 文件
        Task<CosResponseModel> UpFile (string key, string base64);

        // 分块上传大文件
        Task<CosResponseModel> UpBigFile(string key, string srcPath, Action<long, long> progressCallback, Action<CosResult> successCallback, string contentType = null);

        // 查询存储桶的文件列表
        Task<CosResponseModel> SelectObjectList();

        // 下载文件
        Task<CosResponseModel> DownObject(string key, string localDir, string localFileName);

        // 删除文件
        Task<CosResponseModel> DeleteObject(string buketName);
    }

    /// <summary>
    /// Cos客户端
    /// </summary>
    public class CosClient : ICosClient
    {
        CosXmlServer cosXml;
        private readonly string appid;
        public CosClient(CosXmlServer cosXml, string appid)
        {
            this.cosXml = cosXml;
            this.appid = appid;
        }
        public async Task<CosResponseModel> CreateBucket(string buketName)
        {
            try
            {
                string bucket = buketName + "-" + this.appid; //存储桶名称 格式：BucketName-APPID
                PutBucketRequest request = new PutBucketRequest(bucket);
                //设置签名有效时长
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.Seconds), 600);
                //执行请求
                PutBucketResult result = await Task.FromResult(this.cosXml.PutBucket(request));

                return new CosResponseModel { Code = 200, Message = "Success", Data = result.GetResultInfo() };
            }
            catch (CosClientException clientEx)
            {
                return new CosResponseModel { Code = 500, Message = "CosClientException: " + clientEx.Message +clientEx.InnerException};
            }
            catch (CosServerException serverEx)
            {
                return new CosResponseModel { Code = 200, Message = "CosServerException: " + serverEx.GetInfo() };
            }
        }
        public async Task<CosResponseModel> SelectBucket(int tokenTome = 600)
        {
            try
            {
                GetServiceRequest request = new GetServiceRequest();
                //设置签名有效时长
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.Seconds), tokenTome);
                //执行请求
                GetServiceResult result = await Task.FromResult(this.cosXml.GetService(request));
                return new CosResponseModel { Code = 200, Message = "Success", Data = result.GetResultInfo() };
            }
            catch (COSXML.CosException.CosClientException clientEx)
            {
                return new CosResponseModel { Code = 500, Message = "CosClientException: " + clientEx.Message };
            }
            catch (CosServerException serverEx)
            {
                return new CosResponseModel { Code = 500, Message = "CosServerException: " + serverEx.GetInfo() };
            }
        }
    }

    /// <summary>
    /// 存储桶客户端
    /// </summary>
    public class BucketClient : IBucketClient
    {
        private readonly CosXmlServer cosXml;
        private readonly string buketName;
        private readonly string appid;
        public BucketClient(CosXmlServer cosXml, string buketName, string appid)
        {
            this.cosXml = cosXml;
            this.buketName = buketName;
            this.appid = appid;
        }
        public async Task<CosResponseModel> UpFile(string key, string srcPath, string contentType = null)
        {
            try
            {
                string bucket = this.buketName + "-" + this.appid; //存储桶名称 格式：BucketName-APPID
                PutObjectRequest request = new PutObjectRequest(bucket, key, srcPath);         
                //设置签名有效时长
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.Seconds), 600);
                if(!string.IsNullOrEmpty(contentType))
                {
                    //自定义头部 Content-Type
                    request.SetRequestHeader("Content-Type", contentType);
                }
                //设置进度回调
                request.SetCosProgressCallback(delegate (long completed, long total)
                {
                    Console.WriteLine(String.Format("progress = {0:##.##}%", completed * 100.0 / total));
                });
                //执行请求
                PutObjectResult result = await Task.FromResult(this.cosXml.PutObject(request));

                return new CosResponseModel { Code = 200, Message = "Success", Data = result.GetResultInfo() };
            }
            catch (CosClientException clientEx)
            {
                return new CosResponseModel { Code = 500, Message = "CosClientException: " + clientEx.Message };
            }
            catch (CosServerException serverEx)
            {
                return new CosResponseModel { Code = 500, Message = "CosServerException: " + serverEx.GetInfo() };
            }
        }
        public async Task<CosResponseModel> UpFile(string key, string base64)
        {
            try
            {
                string bucket = this.buketName + "-" + this.appid; //存储桶名称 格式：BucketName-APPID
                string base64data = base64.Base64DataResolve(out string contentType);
                Stream stream = base64data.Base64StrToStream();
                PutObjectRequest request = new PutObjectRequest(bucket, key, stream);         
                //设置签名有效时长
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.Seconds), 600);
                if(!string.IsNullOrEmpty(contentType))
                {
                    //自定义头部 Content-Type
                    request.SetRequestHeader("Content-Type", contentType);
                }
                //设置进度回调
                request.SetCosProgressCallback(delegate (long completed, long total)
                {
                    Console.WriteLine(String.Format("progress = {0:##.##}%", completed * 100.0 / total));
                });
                //执行请求
                PutObjectResult result = await Task.FromResult(this.cosXml.PutObject(request));

                return new CosResponseModel { Code = 200, Message = "Success", Data = result.GetResultInfo() };
            }
            catch (CosClientException clientEx)
            {
                return new CosResponseModel { Code = 500, Message = "CosClientException: " + clientEx.Message };
            }
            catch (CosServerException serverEx)
            {
                return new CosResponseModel { Code = 500, Message = "CosServerException: " + serverEx.GetInfo() };
            }
        }
        /// <summary>
        /// 上传大文件、分块上传
        /// </summary>
        /// <param name="key"></param>
        /// <param name="srcPath"></param>
        /// <param name="progressCallback">委托，可用于显示分块信息</param>
        /// <param name="successCallback">委托，当任务成功时回调</param>
        /// <returns></returns>
        public async Task<CosResponseModel> UpBigFile(string key, string srcPath, Action<long, long> progressCallback, Action<CosResult> successCallback, string contentType = null)
        {
            CosResponseModel responseModel = new CosResponseModel();
            string bucket = this.buketName + "-" + this.appid; //存储桶名称 格式：BucketName-APPID

            TransferManager transferManager = new TransferManager(this.cosXml, new TransferConfig());

            COSXMLUploadTask uploadTask = new COSXMLUploadTask(bucket, key);
            if(!string.IsNullOrEmpty(contentType))
            {
                //自定义头部 Content-Type
                PutObjectRequest request = new PutObjectRequest(bucket, key, srcPath);
                request.SetRequestHeader("Content-Type", contentType);
                uploadTask = new COSXMLUploadTask(request);
            }
            uploadTask.SetSrcPath(srcPath);
            uploadTask.progressCallback = delegate (long completed, long total)
            {
                progressCallback(completed, total);
                //Console.WriteLine(String.Format("progress = {0:##.##}%", completed * 100.0 / total));
            };
            uploadTask.successCallback = delegate (CosResult cosResult)
            {
                COSXMLUploadTask.UploadTaskResult result = cosResult as COSXMLUploadTask.UploadTaskResult;
                successCallback(cosResult);
                responseModel.Code = 200;
                responseModel.Message = "Success";
                responseModel.Data = result.GetResultInfo();
            };
            uploadTask.failCallback = delegate (CosClientException clientEx, CosServerException serverEx)
            {
                if (clientEx != null)
                {
                    responseModel.Code = 500;
                    responseModel.Message = clientEx.Message;
                }
                if (serverEx != null)
                {
                    responseModel.Code = 500;
                    responseModel.Message = "CosServerException: " + serverEx.GetInfo();
                }
            };
            await transferManager.UploadAsync(uploadTask);
            return responseModel;
        }

        public async Task<CosResponseModel> SelectObjectList()
        {
            try
            {
                string bucket = this.buketName + "-" + this.appid; //存储桶名称 格式：BucketName-APPID
                GetBucketRequest request = new GetBucketRequest(bucket);
                //设置签名有效时长
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.Seconds), 600);
                //执行请求
                GetBucketResult result = await Task.FromResult(this.cosXml.GetBucket(request));
                return new CosResponseModel { Code = 200, Message = "Success", Data = result.GetResultInfo() };
            }
            catch (CosClientException clientEx)
            {
                return new CosResponseModel { Code = 500, Message = "CosClientException: " + clientEx.Message };
            }
            catch (CosServerException serverEx)
            {
                return new CosResponseModel { Code = 500, Message = "CosServerException: " + serverEx.GetInfo() };
            }
        }
        public async Task<CosResponseModel> DownObject(string key, string localDir, string localFileName)
        {
            try
            {
                string bucket = this.buketName + "-" + this.appid; //存储桶名称 格式：BucketName-APPID
                GetObjectRequest request = new GetObjectRequest(bucket, key, localDir, localFileName);
                //设置签名有效时长
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.Seconds), 600);
                //设置进度回调
                request.SetCosProgressCallback(delegate (long completed, long total)
                {
                    Console.WriteLine(String.Format("progress = {0:##.##}%", completed * 100.0 / total));
                });
                //执行请求
                GetObjectResult result = await Task.FromResult(this.cosXml.GetObject(request));

                return new CosResponseModel { Code = 200, Message = "Success", Data = result.GetResultInfo() };
            }
            catch (CosClientException clientEx)
            {
                return new CosResponseModel { Code = 500, Message = "CosClientException: " + clientEx.Message };
            }
            catch (CosServerException serverEx)
            {
                return new CosResponseModel { Code = 500, Message = serverEx.GetInfo() };
            }
        }
        public async Task<CosResponseModel> DeleteObject(string buketName)
        {
            try
            {
                string bucket = this.buketName + "-" + this.appid; //存储桶名称 格式：BucketName-APPID
                string key = "exampleobject"; //对象在存储桶中的位置，即称对象键.
                DeleteObjectRequest request = new DeleteObjectRequest(bucket, key);
                //设置签名有效时长
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.Seconds), 600);
                //执行请求
                DeleteObjectResult result = await Task.FromResult(this.cosXml.DeleteObject(request));

                return new CosResponseModel { Code = 200, Message = "Success", Data = result.GetResultInfo() };
            }
            catch (CosClientException clientEx)
            {
                return new CosResponseModel { Code = 500, Message = "CosClientException: " + clientEx.Message };
            }
            catch (CosServerException serverEx)
            {
                return new CosResponseModel { Code = 500, Message = "CosServerException: " + serverEx.GetInfo() };
            }
        }
    }

    /// <summary>
    /// 消息响应
    /// </summary>
    public class CosResponseModel
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public dynamic Data { get; set; }
    }
}

// 使用案例
// class Program
// {
//     static async Task Main(string[] args)
//     {
//         // 构建一个 CoxXmlServer 对象
//         var cosClient = new CosBuilder()
//             .SetAccount("1252744", "ap-guangzhou")
//             .SetCosXmlServer()
//             .SetSecret("AKIDEZohWs462rVxLIpG", "Sn1iFi182jMAOE5rSwG")
//             .Builder();
//         // 创建Cos连接客户端
//         ICosClient client = new CosClient(cosClient, "1257544");
//         // 创建一个存储桶
//         var result = await client.CreateBucket("fsdgerer");
//         Console.WriteLine("处理结果：" + result.Message);
//         // 查询存储桶列表
//         var c = await client.SelectBucket();
//         Console.WriteLine(c.Message + c.Data);

//         Console.ReadKey();
//     }
// }

// 官方文档
// https://cloud.tencent.com/document/product/436/6222

// .NET SDK
// https://cloud.tencent.com/document/product/436/32819