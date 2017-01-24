var url = "ws://localhost:9090/dmd";
var typeSet = {
	'jBinary.littleEndian': true,
	gray2: {
		timestamp: 'uint32',
		planes: 'blob'
	},
	gray4: {
		timestamp: 'uint32',
		planes: 'blob'
	},
	coloredGray2: {
		timestamp: 'uint32',
		palette: 'palette',
		planes: 'blob'
	},
	coloredGray4: {
		timestamp: 'uint32',
		palette: 'palette',
		planes: 'blob'
	},
	rgb24: {
		timestamp: 'uint32',
		planes: 'blob'
	},
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
					case 'gray2':
						that.renderGray2(data.data);
						break;
					case 'gray4':
						that.renderGray4(data.data);
						break;
					case 'coloredGray2':
						that.renderColoredGray2(data.data);
						break;
					case 'coloredGray4':
						that.renderColoredGray4(data.data);
						break;
					case 'rgb24':
						that.renderRgb24(data.data);
						break;
					case 'dimensions':
						that.setDimensions(data.data);
						break;
					case 'color':
						that.setColor(data.data.color);
						break;
					case 'palette':
						that.setPalette(data.data.palette);
						break;
					case 'clearColor':
						that.clearColor();
						break;
					case 'clearPalette':
						that.clearPalette();
						break;
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

	renderGray2(frame) {
		//console.log('Gray2 Frame: %s', frame.timestamp);
	},

	renderGray4(frame) {
		console.log('Gray4 Frame: %s', frame.timestamp);
	},

	renderColoredGray2(frame) {
		console.log('Colored Gray 4 Frame: %s', frame.timestamp);
	},

	renderColoredGray4(frame) {
		console.log('Colored Gray 4 Frame: %s', frame.timestamp);
	},

	renderRgb24(frame) {
		console.log('Colored rgb24 Frame: %s', frame.timestamp);
	},

	setDimensions: function(dim) {
		this._width = dim.width;
		this._height = dim.height;
		console.log('New dimensions: %sx%s', this._width, this._height);
	},

	setColor: function(color) {
		this._color = color;
		console.log('New color:', this._color.toString(16));
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