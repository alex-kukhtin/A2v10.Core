

Vue.component("a2-mdi-header", {
	template: `
<div class="mdi-header">
	<span v-text=title></span>
	<div class="aligner"></div>
	<slot></slot>
</div>
	`,
	props: {
		title: String,
		subTitle: String
	},
	mounted() {
		console.dir('header mounted');
		console.dir(this.title);
	}
});
