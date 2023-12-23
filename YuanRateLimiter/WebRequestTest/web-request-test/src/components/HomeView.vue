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
            failedRequests: 0
        }
    },
    methods: {
        requestStart() {
            this.intervalId = setInterval(() => {
                for (let i = 0; i < this.requestRate; i++) {
                    axios({
                        url: this.requestUrl,
                        method: this.requestType,
                        headers: {"X-Forwarded-For": this.imitateIp},
                    }).then(res => {
                        this.successfulRequests++;
                        console.log(res);
                    }).catch(error => {
                        this.failedRequests++;
                        console.error(error);
                    });
                    this.count++;
                }
            }, 1000);
        },
        requestStop() {
            clearInterval(this.intervalId);
        }
    }
}
</script>

<template>
    <el-row>
        <el-col :span="12" :offset="6">
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
                <div style="text-align: center">
                    <el-tag>轮次：{{ count }}</el-tag>&nbsp;
                    <el-tag type="success">成功：{{ successfulRequests }}</el-tag>&nbsp;
                    <el-tag type="danger">失败：{{ failedRequests }}</el-tag>
                </div>
                <div>
                    <div style="text-align: center; margin-top: 20px">
                        <el-button type="primary" v-on:click="requestStart()">开始</el-button>
                        <el-button v-on:click="requestStop()">停止</el-button>
                    </div>
                </div>
            </div>
        </el-col>
    </el-row>
</template>

<style scoped>
.box {
    box-shadow: 0 2px 12px 0 rgba(0, 0, 0, 0.1);
    padding: 20px;
}

.box > div {
    margin-top: 10px;
}

.box > div > p {
    padding-bottom: 5px;
}
</style>