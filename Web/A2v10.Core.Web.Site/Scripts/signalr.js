// Copyright © 2023 Oleksandr Kukhtin. All rights reserved.

/*20230605-8109*/
app.modules['std:signalR'] = function () {

	const eventBus = require('std:eventBus')

	const connection = new signalR.HubConnectionBuilder()
		.withUrl("/_userhub")
		.withAutomaticReconnect()
		.configureLogging(signalR.LogLevel.Information)
		.build();

	connection.on('signal', (event, data) => {
		eventBus.$emit('signalEvent', { event, data })
	});

	return {
		startService,
	};

	async function startService() {
		try {
			await connection.start();
			console.log("SignalR Connected.");
		} catch (err) {
			console.log(err);
			setTimeout(start, 5000);
		}
	}
};
