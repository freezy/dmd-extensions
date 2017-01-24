var url = "ws://localhost:9090/dmd";
var typeSet = {
	'jBinary.littleEndian': true,
	dimensions: {
		width: 'int32',
		height: 'int32'
	},
	color: {
		color: 'int32'
	},
	palette: {
		length: 'int32',
		colors: ['array', 'int32', 'length']
	},
	clearColor: 'blob',
	clearPalette: 'blob',
	coloredgray4: {
		palette: 'palette',
		timestamp: 'uint32',
		planes: 'blob'
	},
	Data: {
		name: 'string0',
		data: jBinary.Template({
			getBaseType: function(context) {
				return context.name;				
			}
		})
	}
};
var defaultColor = 0xff6000;
var controller = {

	init: function() {

		var that = this;
		this._width = 128;
		this._height = 32;
		this._color = defaultColor;
		this._palette = [];

		this.websocket = new ReconnectingWebSocket(url);

		this.websocket.onopen = function (e) {
			that.send('init');
		};

		this.websocket.onmessage = function (e) {
			jBinary.load(e.data, typeSet).then(function (binary) {
				var data = binary.read('Data');
				switch (data.name) {
					case 'dimensions':
						that.setDimensions(data.data);
						break;
					case 'color':
						that.setColor(data.data);
						break;
					case 'palette':
						that.setPalette(data.data);
						break;
					case 'clearColor':
						that.clearColor();
						break;
					case 'clearPalette':
						that.clearPalette();
						break;
					case 'coloredgray4':
						that.renderColoredGray4(data.data);
				}
			});
		};

		this.websocket.onerror = function(e) {
			console.error(e);
		};

		this.websocket.onclose = function (e) {
			console.log('Connection closed');
		};
	},

	renderColoredGray4(frame) {
		console.log('Frame: %s', frame.timestamp);
	},

	setDimensions: function(dim) {
		this._width = dim.width;
		this._height = dim.height;
		console.log('New dimensions: %sx%s', this._width, this._height);
	},

	setColor: function(color) {
		this._color = color;
		console.log('New color: %s', this._color.toString(16));
	},

	setPalette: function(palette) {
		this._palette = palette;
		console.log('New palette with %s colors', this._palette.length);
	},

	clearColor: function () {
		this._color = defaultColor;
		console.log('Color cleared.');
	},

	clearPalette: function () {
		this._palette = null;
		console.log('Palette cleard.');
	},

	send: function (message) {
		this.websocket.send(message);
	}
}

window.addEventListener("load", controller.init.bind(controller), false);