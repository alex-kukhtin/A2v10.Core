﻿/*
This file in the main entry point for defining grunt tasks and using grunt plugins.
Click here to learn more. https://go.microsoft.com/fwlink/?LinkID=513275&clcid=0x409
*/
'use strict';

module.exports = function (grunt) {
	grunt.initConfig({
		terser: {
			options: {
				ecma: '2016',
				mangle: false,
				compress: false
			},
			main: {
				files: {
					'wwwroot/scripts/tabbed.min.js': ['wwwroot/scripts/tabbed.js'],
					'wwwroot/scripts/tabbedsp.min.js': ['wwwroot/scripts/tabbedsp.js']
				}
			}
		},
		watch: {
			files: ["wwwroot/scripts/tabbed.js", "wwwroot/scripts/tabbedsp.js"],
			tasks: ["terser"]
		}
	});

	grunt.loadNpmTasks('grunt-terser');
	grunt.loadNpmTasks('grunt-contrib-watch');
};

