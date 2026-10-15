// Playground for a2-text-editor / a2-text-view. Opened as a file, no server: the hooks are fakes —
// an "upload" is a delay, then an object URL under the stored form test/<n>.

(function () {

	const PIXEL = 'data:image/svg+xml,' + encodeURIComponent(
		'<svg xmlns="http://www.w3.org/2000/svg" width="120" height="60"><rect width="120" height="60" fill="#8ac"/><text x="10" y="36" font-size="16">test/0</text></svg>');

	// a known image, a foreign one (text — a spy pixel), bold, a link, escapes
	const SAMPLE = [
		'Привіт, **світ**!',
		'Посилання: https://example.com/page?id=1.',
		'Своя картинка: ![test0](test/0)',
		'Чужа картинка: ![pixel](https://example.com/p.png)',
		'Екранування: \\*не bold\\*, зворотний \\\\ і непарні **зірки'
	].join('\n');

	let seq = 0;

	new Vue({
		el: '#app',
		data: {
			item: { Text: '' },
			// stored form → URL; reactive: the view re-renders when an upload lands
			images: { 'test/0': PIXEL },
			pending: 0,
			fail: false,
			delay: 1500,
			disabled: false,
			sent: [],
			sample: SAMPLE
		},
		computed: {
			delegates() {
				const vm = this;
				return {
					async UploadImage(file) {
						vm.pending++;
						try {
							await new Promise(resolve => setTimeout(resolve, vm.delay));
							if (vm.fail) {
								console.warn(`upload failed: ${file.name}`);
								return null;
							}
							let src = `test/${++seq}`;
							vm.$set(vm.images, src, URL.createObjectURL(file));
							return src;
						} finally {
							vm.pending--;
						}
					},
					ImageUrl(src) {
						return vm.images[src] || null;
					},
					Submit() {
						vm.send();
					}
				};
			}
		},
		methods: {
			send() {
				if (this.pending || !this.item.Text) return;
				this.sent.unshift({ Text: this.item.Text });
				this.item.Text = '';
			}
		}
	});
})();
