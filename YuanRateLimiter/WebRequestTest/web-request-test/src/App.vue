<template>
    <div id="app">
        <home-view></home-view>
    </div>
</template>

<script>

import HomeView from "@/components/HomeView.vue";

export default {
    name: 'App',
    components: {HomeView}
}
</script>

<style>
* {
    margin: 0;
    padding: 0;
}
#app {
    font-family: Avenir, Helvetica, Arial, sans-serif;
    -webkit-font-smoothing: antialiased;
    -moz-osx-font-smoothing: grayscale;
    color: #2c3e50;
}
</style>
