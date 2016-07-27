#include <stdio.h>
#include <windows.h>

#include "stdafx.h"
#include "boost/interprocess/ipc/message_queue.hpp"

using namespace boost::interprocess;

#if BUILD_DMD_SLAVE
const char* SLAVE_TO_MASTER_MESSAGE_QUEUE_NAME = "dmd_slave_to_master";
const char* MASTER_TO_SLAVE_MESSAGE_QUEUE_NAME = "dmd_master_to_slave";
const char* DOT_MATRIX_DISPLAY_DATA_MESSAGE_QUEUE_NAME = "dmd_dot_matrix_display_data";
#else
const char* SLAVE_TO_MASTER_MESSAGE_QUEUE_NAME = "feedback_slave_to_master";
const char* MASTER_TO_SLAVE_MESSAGE_QUEUE_NAME = "feedback_master_to_slave";
#endif

typedef int32_t s32;
typedef float f32;

enum MESSAGE_TYPE
{
	MESSAGE_TYPE_SLAVE_READY,
	MESSAGE_TYPE_END,
	MESSAGE_TYPE_FEEDBACK
};

enum SOLENOID_ID
{
	SO_PLUNGER = 0,
	SO_TROUGH_EJECT,
	SO_KNOCKER,
	SO_LEFT_SLINGSHOT,
	SO_RIGHT_SLINGSHOT,
	SO_LEFT_JET,
	SO_RIGHT_JET,
	SO_BOTTOM_JET,
	SO_LEFT_DROPS_UP,
	SO_RIGHT_DROPS_UP,
	SO_LOCK_RELEASE_1,
	SO_LOCK_RELEASE_2,
	SO_LOCK_RELEASE_3,
	SO_LOCK_RELEASE_A,
	SO_LOCK_RELEASE_B,
	SO_LOCK_RELEASE_C,
	SO_LOCK_RELEASE_D,
	SO_MIDDLE_EJECT,
	SO_TOP_EJECT_STRONG,
	SO_TOP_EJECT_WEAK,
	SO_MIDDLE_RAMP_DOWN,
	SO_HIGH_DIVERTOR,
	SO_LOW_DIVERTOR,
	SO_SCOOP_RETRACT,
	SO_MAGNO_SAVE,
	SO_MAGNO_LOCK,
	NUM_SOLENOIDS
};

enum FLASHER_ID
{
	FL_LEFT_RETURN_LANE = 0,
	FL_RIGHT_RETURN_LANE,
	FL_TIME_MACHINE,
	FL_LOCK_ALPHA,
	FL_LOCK_BETA,
	FL_LOCK_GAMMA,
	FL_LOCK_DELTA,
	FL_CRYSTAL,
	NUM_FLASHERS
};

enum FLIPPER_ID
{
	FLIP_LOW_LEFT = 0,
	FLIP_LOW_RIGHT,
	FLIP_HIGH_RIGHT,
	NUM_FLIPPERS
};

enum BUTTON_ID
{
	BUTTON_ID_START,
	BUTTON_ID_FIRE,
	BUTTON_ID_MAGNOSAVE,
	NUM_BUTTONS
};

struct FEEDBACK_MESSAGE_DATA
{
	f32 flasher_intensity[NUM_FLASHERS];
	s32 solenoid_on[NUM_SOLENOIDS];
	s32 flipper_solenoid_on[NUM_FLIPPERS];
	s32 button_lit[NUM_BUTTONS];
};

struct SLAVE_MESSAGE
{
	s32 message_type;
	union
	{
		FEEDBACK_MESSAGE_DATA feedback_message_data;
	} message_data;
};

const int DEFAULT_MESSAGE_PRIORITY = 0;

#define LOG printf

const char* SOLENOID_NAME[NUM_SOLENOIDS] =
{
	"PLUNGER",
	"TROUGH_EJECT",
	"KNOCKER",
	"LEFT_SLINGSHOT",
	"RIGHT_SLINGSHOT",
	"LEFT_JET",
	"RIGHT_JET",
	"BOTTOM_JET",
	"LEFT_DROPS_UP",
	"RIGHT_DROPS_UP",
	"LOCK_RELEASE_1",
	"LOCK_RELEASE_2",
	"LOCK_RELEASE_3",
	"LOCK_RELEASE_A",
	"LOCK_RELEASE_B",
	"LOCK_RELEASE_C",
	"LOCK_RELEASE_D",
	"MIDDLE_EJECT",
	"TOP_EJECT_STRONG",
	"TOP_EJECT_WEAK",
	"MIDDLE_RAMP_DOWN",
	"HIGH_DIVERTOR",
	"LOW_DIVERTOR",
	"SCOOP_RETRACT",
	"MAGNO_SAVE",
	"MAGNO_LOCK"
};

const char* FLASHER_NAME[NUM_FLASHERS] =
{
	"LEFT_RETURN_LANE",
	"RIGHT_RETURN_LANE",
	"TIME_MACHINE",
	"LOCK_ALPHA",
	"LOCK_BETA",
	"LOCK_GAMMA",
	"LOCK_DELTA",
	"CRYSTAL"
};

const char* FLIPPER_NAME[NUM_FLIPPERS] =
{
	"LOW_LEFT",
	"LOW_RIGHT",
	"HIGH_RIGHT"
};

const char* BUTTON_NAME[NUM_BUTTONS] =
{
	"START",
	"FIRE",
	"MAGNOSAVE"
};

boost::interprocess::message_queue* open_message_queue(const std::string& message_queue_name)
{
	boost::interprocess::message_queue* message_queue = nullptr;

	try
	{
		message_queue = new boost::interprocess::message_queue(open_only, message_queue_name.c_str());
		LOG("Message queue '%s' opened.\n", message_queue_name.c_str());
	}
	catch (interprocess_exception &exception)
	{
		LOG("Failed to open message queue '%s', error: '%s'\n", message_queue_name.c_str(), exception.what());
		exit(1);
	}

	return message_queue;
}

void handle_feedback(const FEEDBACK_MESSAGE_DATA* feedback_data)
{
	static FEEDBACK_MESSAGE_DATA previous_feedback_data;

	for (int flasher_index = 0; flasher_index < NUM_FLASHERS; flasher_index++)
	{
		if (feedback_data->flasher_intensity[flasher_index] != previous_feedback_data.flasher_intensity[flasher_index])
		{
			LOG("Flasher %s: %f\n", FLASHER_NAME[flasher_index], feedback_data->flasher_intensity[flasher_index]);
		}
	}

	for (int solenoid_index = 0; solenoid_index < NUM_SOLENOIDS; solenoid_index++)
	{
		if (feedback_data->solenoid_on[solenoid_index] != previous_feedback_data.solenoid_on[solenoid_index])
		{
			LOG("Solenoid %s: %s\n", SOLENOID_NAME[solenoid_index], feedback_data->solenoid_on[solenoid_index] ? "ON" : "OFF");
		}
	}

	for (int flipper_index = 0; flipper_index < NUM_FLIPPERS; flipper_index++)
	{
		if (feedback_data->flipper_solenoid_on[flipper_index] != previous_feedback_data.flipper_solenoid_on[flipper_index])
		{
			LOG("Flipper %s: %s\n", FLIPPER_NAME[flipper_index], feedback_data->flipper_solenoid_on[flipper_index] ? "ON" : "OFF");
		}
	}

	for (int button_index = 0; button_index < NUM_BUTTONS; button_index++)
	{
		if (feedback_data->button_lit[button_index] != previous_feedback_data.button_lit[button_index])
		{
			LOG("Button light %s: %s\n", BUTTON_NAME[button_index], feedback_data->button_lit[button_index] ? "ON" : "OFF");
		}
	}

	previous_feedback_data = *feedback_data;
}

int main(int argc, char* argv[])
{
	int general_message_size = 0;

	for (int arg_index = 1; arg_index < argc; arg_index++)
	{
		const char* arg = argv[arg_index];

		const char MESSAGE_SIZE_OPTION = 'm';

		if (arg[0] == MESSAGE_SIZE_OPTION)
		{
			const char* arg_value = &(arg[1]);
			general_message_size = atoi(arg_value);
			LOG("Message size %d\n", general_message_size);
		}
	}

	if (general_message_size == 0)
	{
		LOG("Message size argument missing\n");
		exit(1);
	}

	boost::interprocess::message_queue* master_to_slave_message_queue = open_message_queue(MASTER_TO_SLAVE_MESSAGE_QUEUE_NAME);
	boost::interprocess::message_queue* slave_to_master_message_queue = open_message_queue(SLAVE_TO_MASTER_MESSAGE_QUEUE_NAME);
#if BUILD_DMD_SLAVE
	boost::interprocess::message_queue* dot_matrix_display_data_message_queue = open_message_queue(DOT_MATRIX_DISPLAY_DATA_MESSAGE_QUEUE_NAME);
#endif

	unsigned char* general_message_buffer = new unsigned char[general_message_size];
	SLAVE_MESSAGE* message = (SLAVE_MESSAGE*)general_message_buffer;

	if (slave_to_master_message_queue)
	{
		try
		{
			LOG("Sending slave ready message\n");

			message->message_type = MESSAGE_TYPE_SLAVE_READY;
			slave_to_master_message_queue->send(message, general_message_size, DEFAULT_MESSAGE_PRIORITY);
		}
		catch (interprocess_exception &exception)
		{
			LOG("Error sending slave ready message: %s\n", exception.what());
		}
	}

#if BUILD_DMD_SLAVE
	const int DOT_MATRIX_WIDTH = 128;
	const int DOT_MATRIX_HEIGHT = 32;
	const int DOT_MATRIX_DATA_MESSAGE_SIZE = DOT_MATRIX_WIDTH*DOT_MATRIX_HEIGHT;

	unsigned char* dot_matrix_data_message_buffer = new unsigned char[DOT_MATRIX_DATA_MESSAGE_SIZE];
#endif

	bool done = false;

	while (!done)
	{
#if BUILD_DMD_SLAVE
		if (dot_matrix_display_data_message_queue)
		{
			bool received_message = false;

			do
			{
				try
				{
					unsigned int priority;
					message_queue::size_type received_size;

					const boost::posix_time::ptime wait_time = microsec_clock::universal_time() + boost::posix_time::milliseconds(500);

					received_message = dot_matrix_display_data_message_queue->timed_receive(dot_matrix_data_message_buffer,
						DOT_MATRIX_DATA_MESSAGE_SIZE,
						received_size,
						priority,
						wait_time);

					if (received_message)
					{
						if (received_size == DOT_MATRIX_DATA_MESSAGE_SIZE)
						{
							LOG("Received dmd data message!\n");
						}
						else
						{
							LOG("Received dmd data message size %d, but expecting size %d\n", (int)received_size, DOT_MATRIX_DATA_MESSAGE_SIZE);
						}
					}
				}
				catch (interprocess_exception &exception)
				{
					LOG("Error receiving dot matrix display data message: '%s'\n", exception.what());
				}
			} while (received_message);
		}
#endif

		if (master_to_slave_message_queue)
		{
			bool received_message = false;

			do
			{
				try
				{
					unsigned int priority;
					message_queue::size_type received_size;

					received_message = master_to_slave_message_queue->try_receive(general_message_buffer,
						general_message_size,
						received_size,
						priority);

					if (received_message)
					{
						if (received_size == general_message_size)
						{
							if (message->message_type == MESSAGE_TYPE_END)
							{
								LOG("Received end message\n");
								done = true;
							}
							else if (message->message_type == MESSAGE_TYPE_FEEDBACK)
							{
								handle_feedback(&(message->message_data.feedback_message_data));
							}
							else
							{
								LOG("Received message %d\n", message->message_type);
							}
						}
						else
						{
							LOG("Received message size %d, but expecting size %d\n", (int)received_size, general_message_size);
						}
					}
				}
				catch (interprocess_exception &exception)
				{
					LOG("Error receiving message: '%s'\n", exception.what());
				}
			} while (received_message);
		}
/*
#if !BUILD_DMD_SLAVE
		const DWORD SLEEP_TIME_MILLI_SECONDS = 5;
		Sleep(SLEEP_TIME_MILLI_SECONDS);
#endif*/
	}

	return 0;
}

