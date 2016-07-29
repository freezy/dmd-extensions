#include "stdafx.h"
#include "ProPinballBridge.h"
#include "boost/interprocess/ipc/message_queue.hpp"
#include <stdio.h>

using namespace boost::interprocess;

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


ProPinballBridge::ProPinballDmd::ProPinballDmd(unsigned int message_size)
{
	const char* DMD_DATA_QUEUE_NAME = "dmd_dot_matrix_display_data";
	dmd_data_message_queue = open_message_queue(DMD_DATA_QUEUE_NAME);
	dot_matrix_data_message_buffer = new unsigned char[128*32];

	const char* MASTER_TO_SLAVE_QUEUE_NAME = "dmd_master_to_slave";
	master_to_slave_message_queue = open_message_queue(MASTER_TO_SLAVE_QUEUE_NAME);

	const char* SLAVE_TO_MASTER_QUEUE_NAME = "dmd_slave_to_master";
	slave_to_master_message_queue = open_message_queue(SLAVE_TO_MASTER_QUEUE_NAME);

	general_message_buffer_size = message_size; // Get from command line arg m
	general_message_buffer = new unsigned char[general_message_buffer_size];

	SLAVE_MESSAGE* message = (SLAVE_MESSAGE*)general_message_buffer;

	if (slave_to_master_message_queue)
	{
		try
		{
			message->message_type = MESSAGE_TYPE_SLAVE_READY;
			slave_to_master_message_queue->send(message, general_message_buffer_size, DEFAULT_MESSAGE_PRIORITY);
		}
		catch (interprocess_exception &exception)
		{
			Status = 1;
			Error = exception.what();
			printf("Error sending slave ready message: %s\n", exception.what());
		}
	}
}

void ProPinballBridge::ProPinballDmd::Release()
{
	delete this;
}

void ProPinballBridge::ProPinballDmd::GetFrames(OnNext^ onNext, OnError^ onError, OnCompleted^ onCompleted)
{
	bool done = false;

	while (!done)
	{
		if (dmd_data_message_queue)
		{
			bool received_message = false;
			do
			{
				try
				{
					unsigned int priority;
					message_queue::size_type received_size;

					const boost::posix_time::ptime wait_time = microsec_clock::universal_time() + boost::posix_time::milliseconds(500);

					received_message = dmd_data_message_queue->timed_receive(dot_matrix_data_message_buffer,
						128 * 32,
						received_size,
						priority,
						wait_time);

					if (received_message)
					{
						if (received_size == 128 * 32)
						{
							onNext(dot_matrix_data_message_buffer);
						}
						else
						{
							onError("Received DMD data has wrong size.");
							received_message = false;
							done = true;
						}
					}

				}
				catch (interprocess_exception &exception)
				{
					onError(exception.what());
					done = true;
				}
			} while (received_message);
		}
		else
		{
			onError("Invalid message queue.");
			done = true;
		}

		if (master_to_slave_message_queue)
		{
			SLAVE_MESSAGE* message = (SLAVE_MESSAGE*)general_message_buffer;
			bool received_message = false;

			do
			{
				try
				{
					unsigned int priority;
					message_queue::size_type received_size;

					received_message = master_to_slave_message_queue->try_receive(general_message_buffer,
																									  general_message_buffer_size,
																									  received_size,
																									  priority);
					if (received_message)
					{
						if (received_size == general_message_buffer_size)
						{
							if (message->message_type == MESSAGE_TYPE_END)
							{
								onCompleted();
								done = true;
							}
							/*
							else if (message->message_type == MESSAGE_TYPE_FEEDBACK)
							{
								printf("Ignoring feedback message."); // handle_feedback(&(message->message_data.feedback_message_data));
							}
							else
							{
								printf("Received message %d\n", message->message_type);
							}*/
						}
						else
						{
							printf("Received message size %d, but expecting size %d\n", (int)received_size, general_message_buffer_size);
							onError("Received DMD data has wrong size.");
							received_message = false;
							done = true;
						}
					}
				}
				catch (interprocess_exception &exception)
				{
					onError("Error receiving control message.");
					printf("Control message error: %s\n", exception.what());
					done = true;
					received_message = false;
				}
			} while (received_message);
		}
	}
}

boost::interprocess::message_queue* ProPinballBridge::ProPinballDmd::open_message_queue(const std::string& message_queue_name)
{
	boost::interprocess::message_queue* message_queue = nullptr;

	try
	{
		message_queue = new boost::interprocess::message_queue(open_only, message_queue_name.c_str());
	}
	catch (interprocess_exception &exception)
	{
		printf("+++ Failed to open message queue '%s', error: '%s'\n", message_queue_name.c_str(), exception.what());
		Status = 1;
		Error = exception.what();
	}
	return message_queue;
}