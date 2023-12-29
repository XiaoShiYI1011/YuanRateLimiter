<!--
    组件名：
    描述：
    创建人：十一
    创建时间：2023/12/20 21:24:59
-->
<script>
import axios from "axios";

export default {
    data() {
        return {
            requestUrl: "http://localhost:5155/api/Test/Test01",
            requestType: "GET",
            requestRate: 1,
            imitateIp: "171.113.42.210",
            intervalId: null,
            count: 0,
            successfulRequests: 0,
            failedRequests: 0,
            timekeeping: 0,
            requestResultList: [],
        }
    },
    methods: {
        requestStart() {
            this.intervalId = setInterval(async () => {
                const requests = [];
                for (let i = 0; i < this.requestRate; i++) {
                    const requestPromise = axios({
                        url: this.requestUrl,
                        method: this.requestType,
                        headers: {"X-Forwarded-For": this.imitateIp},
                    }).then(res => {
                        this.successfulRequests++;
                        this.requestResultList.push({
                            statusCode: res.status,
                            responseMsg: res.data
                        });
                    }).catch(error => {
                        this.failedRequests++;
                        this.requestResultList.push({
                            statusCode: error.response.status,
                            responseMsg: error.response.data,
                        });
                    });
                    requests.push(requestPromise);
                    this.count++;
                }
                try {
                    await Promise.all(requests);
                } catch (error) {
                    console.error("Error in Promise.all:", error);
                }
                this.timekeeping++;
            }, 1000);
        },
        requestStop() {
            clearInterval(this.intervalId);
        },
        resetting() {
            this.requestUrl = "http://localhost:5155/api/Test/Test01";
            this.requestType = "GET";
            this.requestRate = 1;
            this.imitateIp = "171.113.42.210";
            this.count = 0;
            this.successfulRequests = 0
            this.failedRequests = 0;
            this.timekeeping = 0;
            this.requestResultList.length = 0;
        },
    }
}
</script>

<template>
    <el-row>
        <el-col :span="12">
            <div class="box">
                <div>
                    <p>请求地址：</p>
                    <el-input v-model="requestUrl"></el-input>
                </div>
                <div>
                    <p>请求方法：</p>
                    <el-input v-model="requestType"></el-input>
                </div>
                <div>
                    <p>模拟Ip：</p>
                    <el-input v-model="imitateIp"></el-input>
                </div>
                <div>
                    <p>请求速率（每秒多少个请求）：</p>
                    <el-input v-model="requestRate"></el-input>
                </div>
                <div>
                    <div style="text-align: right; margin-top: 20px">
                        <el-button type="primary" v-on:click="requestStart()">开始</el-button>
                        <el-button v-on:click="requestStop()">停止</el-button>
                        <el-button v-on:click="resetting()">重置</el-button>
                    </div>
                </div>
            </div>
        </el-col>
        <el-col :span="12">
            <div class="box">
                <div>
                    <el-tag type="info">运行时长 {{ timekeeping }} 秒</el-tag>&nbsp;
                    <el-tag>轮次：{{ count }}</el-tag>&nbsp;
                    <el-tag type="success">成功：{{ successfulRequests }}</el-tag>&nbsp;
                    <el-tag type="danger">失败：{{ failedRequests }}</el-tag>
                </div>
                <div class="result-table">
                    <el-table
                        :data="requestResultList"
                        stripe
                        style="width: 100%"
                        max-height="600">
                        <el-table-column label="序号" width="70" align="center" type="index"></el-table-column>
                        <el-table-column prop="statusCode" label="状态码" width="100" align="center">
                        </el-table-column>
                        <el-table-column prop="responseMsg" label="响应消息"></el-table-column>
                    </el-table>
                </div>
            </div>
        </el-col>
    </el-row>
</template>

<style scoped>
.box {
    padding: 5px 20px 20px 20px;
    min-height: 96.55vh;
    max-height: 96.55vh;
}

.box:first-child {
    border-right: 1px solid #ccc;
}

.box > div {
    margin-top: 10px;
}

.box > div > p {
    padding-bottom: 5px;
}

.result-table {
    box-shadow: 0 2px 4px rgba(0, 0, 0, .12), 0 0 6px rgba(0, 0, 0, .04);
    min-height: 90vh;
}
</style>