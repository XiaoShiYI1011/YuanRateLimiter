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
            intervalId: null,
        }
    },
    methods: {
        requestStart() {
            this.intervalId = setInterval(() => {
                for (let i = 0; i < this.requestRate; i++) {
                    axios({
                        url: this.requestUrl,
                        method: this.requestType
                    }).then(res => {
                        console.log(res);
                    }).catch(error => {
                        console.error(error);
                    });
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
                    <p>请求速率（每秒多少个请求）：</p>
                    <el-input v-model="requestRate"></el-input>
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